using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using PixelDiff.Capture.Windows.Interop;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;

namespace PixelDiff.Capture.Windows;

/// <summary>
/// Captures the screen and individual windows through GDI. Regions are copied from the desktop
/// device context, windows are rendered with <c>PrintWindow</c>. The host process should be per
/// monitor DPI aware, otherwise Windows hands out virtualised coordinates that do not line up
/// with the pixels that are actually copied.
/// </summary>
public sealed class WindowsScreenCapture : IScreenCapture
{
    // Titles longer than this are truncated. Real captions are far shorter, and a fixed ceiling
    // keeps a hostile or broken window from forcing a large allocation during enumeration.
    private const int MaxTitleLength = 1024;

    /// <inheritdoc />
    /// <remarks>
    /// The origin is negative when a monitor sits above or to the left of the primary monitor,
    /// because the primary monitor always anchors the coordinate system at zero.
    /// </remarks>
    public CaptureRegion VirtualScreenBounds => new(
        NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
        NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));

    /// <inheritdoc />
    public IReadOnlyList<CapturableWindow> EnumerateWindows()
    {
        var windows = new List<CapturableWindow>();

        bool Visit(nint handle, nint parameter)
        {
            CapturableWindow? window = DescribeWindow(handle);
            if (window is not null)
            {
                windows.Add(window);
            }

            return true;
        }

        // The delegate must stay reachable until the native call returns, otherwise the garbage
        // collector may free the thunk while Windows is still calling back into it.
        var callback = new NativeMethods.EnumWindowsProc(Visit);
        bool completed = NativeMethods.EnumWindows(callback, 0);
        GC.KeepAlive(callback);

        if (!completed)
        {
            throw new ScreenCaptureException(
                "Window enumeration failed.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return windows;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This copies the composited desktop, so a window that overlaps the region appears in the
    /// result. Use <see cref="CaptureWindow"/> to capture a window that may be covered.
    /// </remarks>
    public Mat CaptureScreenRegion(CaptureRegion region)
    {
        region.EnsureNotEmpty();

        CaptureRegion visible = region.IntersectWith(VirtualScreenBounds);
        if (visible.IsEmpty)
        {
            throw new ScreenCaptureException(
                $"The region {region.X},{region.Y} {region.Width}x{region.Height} lies outside the virtual screen.");
        }

        nint desktop = NativeMethods.GetDesktopWindow();
        nint desktopDc = NativeMethods.GetWindowDC(desktop);
        if (desktopDc == 0)
        {
            throw LastError("The desktop device context could not be opened.");
        }

        try
        {
            return RenderToMat(
                desktopDc,
                visible.Width,
                visible.Height,
                memoryDc => NativeMethods.BitBlt(
                    memoryDc,
                    0,
                    0,
                    visible.Width,
                    visible.Height,
                    desktopDc,
                    visible.X,
                    visible.Y,
                    NativeMethods.SRCCOPY),
                "The screen region could not be copied.");
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(desktop, desktopDc);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="crop"/> is measured from the top left of the visible frame, not from the
    /// invisible resize border that Windows adds around most top level windows, so the numbers
    /// match what a person sees on screen. The crop is clipped to the captured size.
    /// </remarks>
    public Mat CaptureWindow(nint windowHandle, CaptureRegion? crop = null)
    {
        if (!NativeMethods.IsWindow(windowHandle))
        {
            throw new ScreenCaptureException($"Handle 0x{windowHandle:X} does not refer to a window.");
        }

        // A minimised window has no client surface to render, and PrintWindow would succeed with a blank image.
        if (NativeMethods.IsIconic(windowHandle))
        {
            throw new ScreenCaptureException($"Window 0x{windowHandle:X} is minimised and cannot be captured.");
        }

        if (!NativeMethods.GetWindowRect(windowHandle, out NativeMethods.RECT outer))
        {
            throw LastError($"The bounds of window 0x{windowHandle:X} could not be read.");
        }

        CaptureRegion outerBounds = ToRegion(outer);
        if (outerBounds.IsEmpty)
        {
            throw new ScreenCaptureException($"Window 0x{windowHandle:X} has no visible area.");
        }

        // PrintWindow renders the full outer rectangle, so the visible frame is located inside
        // that bitmap and everything outside it is discarded.
        CaptureRegion frame = ReadVisibleBounds(windowHandle, outer);
        var imageBounds = new CaptureRegion(0, 0, outerBounds.Width, outerBounds.Height);
        CaptureRegion keep = new CaptureRegion(frame.X - outer.Left, frame.Y - outer.Top, frame.Width, frame.Height)
            .IntersectWith(imageBounds);

        if (crop is { } requested)
        {
            keep = new CaptureRegion(keep.X + requested.X, keep.Y + requested.Y, requested.Width, requested.Height)
                .IntersectWith(keep);
        }

        if (keep.IsEmpty)
        {
            throw new ScreenCaptureException($"The requested crop lies outside window 0x{windowHandle:X}.");
        }

        nint windowDc = NativeMethods.GetWindowDC(windowHandle);
        if (windowDc == 0)
        {
            throw LastError($"The device context of window 0x{windowHandle:X} could not be opened.");
        }

        try
        {
            using Mat full = RenderToMat(
                windowDc,
                outerBounds.Width,
                outerBounds.Height,
                memoryDc => NativeMethods.PrintWindow(windowHandle, memoryDc, NativeMethods.PW_RENDERFULLCONTENT),
                $"Window 0x{windowHandle:X} could not be rendered.");

            using var view = new Mat(full, new Rect(keep.X, keep.Y, keep.Width, keep.Height));
            return view.Clone();
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(windowHandle, windowDc);
        }
    }

    /// <inheritdoc />
    public Mat Capture(CaptureTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();

        switch (target.Kind)
        {
            case CaptureTargetKind.ScreenRegion:
                return CaptureScreenRegion(target.Region!.Value);

            case CaptureTargetKind.Window:
                string pattern = target.WindowTitlePattern!;
                CapturableWindow? match = EnumerateWindows()
                    .FirstOrDefault(window => window.Title.Contains(pattern, StringComparison.OrdinalIgnoreCase));

                return match is null
                    ? throw new ScreenCaptureException($"No visible window has a title containing '{pattern}'.")
                    : CaptureWindow(match.Handle, target.Region);

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unknown capture target kind.");
        }
    }

    /// <summary>
    /// Draws into a fresh bitmap through a memory device context and converts the result. Every
    /// handle is released in a finally block because leaked device contexts and bitmaps count
    /// against the per process GDI quota, and a long running tool would eventually be unable to
    /// draw anything.
    /// </summary>
    private static Mat RenderToMat(
        nint sourceDc,
        int width,
        int height,
        Func<nint, bool> draw,
        string failureMessage)
    {
        nint memoryDc = NativeMethods.CreateCompatibleDC(sourceDc);
        if (memoryDc == 0)
        {
            throw LastError("A memory device context could not be created.");
        }

        try
        {
            nint bitmap = NativeMethods.CreateCompatibleBitmap(sourceDc, width, height);
            if (bitmap == 0)
            {
                throw LastError($"A {width}x{height} bitmap could not be allocated.");
            }

            try
            {
                nint previous = NativeMethods.SelectObject(memoryDc, bitmap);
                if (previous == 0)
                {
                    throw new ScreenCaptureException("The capture bitmap could not be selected into a device context.");
                }

                bool drawn;
                try
                {
                    drawn = draw(memoryDc);
                }
                catch
                {
                    _ = NativeMethods.SelectObject(memoryDc, previous);
                    throw;
                }

                // A bitmap cannot be read back while it is still selected into a device context.
                _ = NativeMethods.SelectObject(memoryDc, previous);

                if (!drawn)
                {
                    throw LastError(failureMessage);
                }

                using Bitmap managed = Image.FromHbitmap(bitmap);
                using Mat raw = BitmapConverter.ToMat(managed);
                return ImageCodec.ToBgr(raw);
            }
            finally
            {
                _ = NativeMethods.DeleteObject(bitmap);
            }
        }
        finally
        {
            _ = NativeMethods.DeleteDC(memoryDc);
        }
    }

    /// <summary>Builds a listing entry, or returns null for windows that should not be offered.</summary>
    private static CapturableWindow? DescribeWindow(nint handle)
    {
        // A minimised window keeps its visible style bit and reports a parked rectangle out at
        // -32000, so the visibility test alone lets it through. CaptureWindow then refuses it,
        // which would leave the list offering targets that cannot be captured.
        if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.IsIconic(handle))
        {
            return null;
        }

        string title = ReadTitle(handle);
        if (title.Length == 0 || !NativeMethods.GetWindowRect(handle, out NativeMethods.RECT outer))
        {
            return null;
        }

        CaptureRegion bounds = ReadVisibleBounds(handle, outer);
        if (bounds.IsEmpty)
        {
            return null;
        }

        return new CapturableWindow(handle, title, bounds, ReadProcessName(handle));
    }

    private static unsafe string ReadTitle(nint handle)
    {
        int length = Math.Min(NativeMethods.GetWindowTextLengthW(handle), MaxTitleLength);
        if (length <= 0)
        {
            return string.Empty;
        }

        // One extra slot for the terminator that the API always writes.
        char* buffer = stackalloc char[length + 1];
        int copied = NativeMethods.GetWindowTextW(handle, buffer, length + 1);
        return copied > 0 ? new string(buffer, 0, copied) : string.Empty;
    }

    /// <summary>
    /// Prefers the DWM frame bounds. <c>GetWindowRect</c> includes an invisible resize border on
    /// current Windows versions, which would pull a few pixels of whatever lies behind the window
    /// into the capture. The plain rectangle is the fallback for when composition is unavailable.
    /// </summary>
    private static CaptureRegion ReadVisibleBounds(nint handle, NativeMethods.RECT fallback)
    {
        int result = NativeMethods.DwmGetWindowAttribute(
            handle,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT frame,
            Marshal.SizeOf<NativeMethods.RECT>());

        return ToRegion(result == 0 ? frame : fallback);
    }

    private static string ReadProcessName(nint handle)
    {
        if (NativeMethods.GetWindowThreadProcessId(handle, out uint processId) == 0)
        {
            return string.Empty;
        }

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // The process exited between enumeration and lookup, or is protected from this user.
            // The name is informational, so an empty value is better than failing the whole listing.
            return string.Empty;
        }
    }

    private static CaptureRegion ToRegion(NativeMethods.RECT rect) =>
        CaptureRegion.FromEdges(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static ScreenCaptureException LastError(string message) =>
        new(message, new Win32Exception(Marshal.GetLastPInvokeError()));
}
