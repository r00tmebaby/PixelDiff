using System.Runtime.InteropServices;

namespace PixelDiff.Capture.Windows.Interop;

/// <summary>
/// Win32 entry points used for capture. Source generated marshalling is used so the stubs are
/// visible at compile time and friendly to trimming. Calls whose failure is meaningful set the
/// last error so the caller can report the explanation the system itself gives.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>Left edge of the virtual screen, which can be negative.</summary>
    internal const int SM_XVIRTUALSCREEN = 76;

    /// <summary>Top edge of the virtual screen, which can be negative.</summary>
    internal const int SM_YVIRTUALSCREEN = 77;

    /// <summary>Width of the virtual screen.</summary>
    internal const int SM_CXVIRTUALSCREEN = 78;

    /// <summary>Height of the virtual screen.</summary>
    internal const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>DWM attribute for the visible frame, without the invisible resize border.</summary>
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    /// <summary>Asks <see cref="PrintWindow"/> to render the full content, including hardware accelerated surfaces.</summary>
    internal const uint PW_RENDERFULLCONTENT = 2;

    /// <summary>Raster operation that copies the source rectangle unchanged.</summary>
    internal const uint SRCCOPY = 0x00CC0020;

    /// <summary>Callback invoked once per top level window; return false to stop enumerating.</summary>
    /// <param name="windowHandle">The window being visited.</param>
    /// <param name="parameter">The value passed to <see cref="EnumWindows"/>.</param>
    /// <returns>True to continue.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(
        [MarshalAs(UnmanagedType.FunctionPtr)] EnumWindowsProc callback,
        nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint windowHandle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint windowHandle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint windowHandle);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetWindowTextLengthW(nint windowHandle);

    // Characters are written straight into the caller's buffer, which avoids a string allocation per window.
    [LibraryImport("user32.dll", SetLastError = true)]
    internal static unsafe partial int GetWindowTextW(nint windowHandle, char* text, int maxCount);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint windowHandle, out RECT rect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(nint windowHandle, nint deviceContext, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint GetWindowDC(nint windowHandle);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint windowHandle, nint deviceContext);

    [LibraryImport("user32.dll")]
    internal static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateCompatibleDC(nint deviceContext);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateCompatibleBitmap(nint deviceContext, int width, int height);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint deviceContext, nint graphicsObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint graphicsObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint deviceContext);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(
        nint destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        nint source,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    // The return value is an HRESULT rather than a BOOL, so success is zero.
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(nint windowHandle, int attribute, out RECT value, int size);

    /// <summary>Win32 rectangle expressed as edges rather than origin and size.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        /// <summary>Left edge.</summary>
        public int Left;

        /// <summary>Top edge.</summary>
        public int Top;

        /// <summary>Right edge, exclusive.</summary>
        public int Right;

        /// <summary>Bottom edge, exclusive.</summary>
        public int Bottom;
    }
}
