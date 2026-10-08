using PixelDiff.Core.Models;

namespace PixelDiff.Capture.Windows;

/// <summary>
/// Describes a top level window that can be captured. The handle is only meaningful while the
/// window exists, so a value should be used soon after enumeration rather than stored.
/// </summary>
/// <param name="Handle">The native window handle.</param>
/// <param name="Title">The window caption, which is never empty for an enumerated window.</param>
/// <param name="Bounds">The visible frame in virtual screen coordinates, excluding the invisible resize border.</param>
/// <param name="ProcessName">Name of the owning process, or an empty string when it could not be resolved.</param>
public sealed record CapturableWindow(nint Handle, string Title, CaptureRegion Bounds, string ProcessName)
{
    /// <summary>Formats the window as its title followed by its size, for display in a picker list.</summary>
    /// <returns>For example <c>Calculator (320x480)</c>.</returns>
    public override string ToString() => $"{Title} ({Bounds.Width}x{Bounds.Height})";
}
