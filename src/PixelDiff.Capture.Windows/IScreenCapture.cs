using OpenCvSharp;
using PixelDiff.Core.Models;

namespace PixelDiff.Capture.Windows;

/// <summary>
/// Source of screenshots for the comparer. It is an interface so the engine and the UI can be
/// exercised against canned images without a desktop session. Every <see cref="Mat"/> returned
/// by a member is a three channel BGR image that the caller owns and must dispose.
/// </summary>
public interface IScreenCapture
{
    /// <summary>
    /// Bounding rectangle of all monitors together. The origin is negative when a monitor sits
    /// above or to the left of the primary one.
    /// </summary>
    CaptureRegion VirtualScreenBounds { get; }

    /// <summary>Lists the visible, titled top level windows that can be captured.</summary>
    /// <returns>A snapshot in z-order; windows can close before they are used.</returns>
    IReadOnlyList<CapturableWindow> EnumerateWindows();

    /// <summary>Captures a rectangle of the composited desktop.</summary>
    /// <param name="region">The rectangle in virtual screen coordinates; it is clipped to the screen.</param>
    /// <returns>A BGR image the caller must dispose.</returns>
    /// <exception cref="ScreenCaptureException">The region is off screen or the system refused the capture.</exception>
    Mat CaptureScreenRegion(CaptureRegion region);

    /// <summary>Captures a single window regardless of what covers it.</summary>
    /// <param name="windowHandle">The native window handle.</param>
    /// <param name="crop">Optional rectangle relative to the window's top left corner.</param>
    /// <returns>A BGR image the caller must dispose.</returns>
    /// <exception cref="ScreenCaptureException">The handle is not a window or the capture failed.</exception>
    Mat CaptureWindow(nint windowHandle, CaptureRegion? crop = null);

    /// <summary>Captures whatever a <see cref="CaptureTarget"/> describes.</summary>
    /// <param name="target">The region or window to capture.</param>
    /// <returns>A BGR image the caller must dispose.</returns>
    /// <exception cref="ScreenCaptureException">No window matches or the capture failed.</exception>
    Mat Capture(CaptureTarget target);
}
