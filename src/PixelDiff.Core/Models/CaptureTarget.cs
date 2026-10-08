namespace PixelDiff.Core.Models;

/// <summary>
/// Describes how to reacquire the same image on a later run. Storing the target alongside the
/// image is what lets an unattended run recapture a baseline without being told where to look.
/// </summary>
public sealed record CaptureTarget
{
    /// <summary>Selects which of the other members carries the location.</summary>
    public required CaptureTargetKind Kind { get; init; }

    /// <summary>
    /// The rectangle to grab for <see cref="CaptureTargetKind.ScreenRegion"/>. For a window target
    /// this is an optional crop applied relative to the window's top left corner.
    /// </summary>
    public CaptureRegion? Region { get; init; }

    /// <summary>
    /// Case insensitive substring matched against window titles. Only meaningful for
    /// <see cref="CaptureTargetKind.Window"/>.
    /// </summary>
    public string? WindowTitlePattern { get; init; }

    /// <summary>Targets a fixed rectangle of the desktop.</summary>
    public static CaptureTarget ForRegion(CaptureRegion region)
    {
        region.EnsureNotEmpty();
        return new CaptureTarget { Kind = CaptureTargetKind.ScreenRegion, Region = region };
    }

    /// <summary>Targets the first visible top level window whose title contains the pattern.</summary>
    /// <param name="titlePattern">Substring to look for. Leading and trailing spaces are trimmed.</param>
    /// <param name="crop">Optional crop relative to the window's top left corner.</param>
    /// <exception cref="ArgumentException">The pattern is empty once trimmed.</exception>
    public static CaptureTarget ForWindow(string titlePattern, CaptureRegion? crop = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titlePattern);
        return new CaptureTarget
        {
            Kind = CaptureTargetKind.Window,
            WindowTitlePattern = titlePattern.Trim(),
            Region = crop,
        };
    }

    /// <summary>
    /// Throws when the members do not agree with <see cref="Kind"/>, which happens when a
    /// descriptor has been hand edited on disk.
    /// </summary>
    /// <exception cref="InvalidOperationException">The target is internally inconsistent.</exception>
    public void Validate()
    {
        switch (Kind)
        {
            case CaptureTargetKind.ScreenRegion when Region is null:
                throw new InvalidOperationException("A screen region target requires a region.");

            case CaptureTargetKind.Window when string.IsNullOrWhiteSpace(WindowTitlePattern):
                throw new InvalidOperationException("A window target requires a title pattern.");

            case CaptureTargetKind.ScreenRegion:
            case CaptureTargetKind.Window:
                break;

            default:
                throw new InvalidOperationException($"Unknown capture target kind '{Kind}'.");
        }

        Region?.EnsureNotEmpty();
    }

    /// <summary>Describes the target the way the user interface lists it.</summary>
    public override string ToString() => Kind switch
    {
        CaptureTargetKind.Window => $"window matching \"{WindowTitlePattern}\"",
        _ => $"screen region {Region}",
    };
}
