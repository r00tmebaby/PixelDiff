using PixelDiff.Core.Models;

namespace PixelDiff.Core.Comparison;

/// <summary>
/// What one comparison measured. Carries no image data so it can be serialised straight into a
/// run report and read by a build server.
/// </summary>
public sealed record ComparisonResult
{
    /// <summary>The pass or fail verdict.</summary>
    public required ComparisonOutcome Outcome { get; init; }

    /// <summary>Pixels that moved by more than the configured tolerance.</summary>
    public required long ChangedPixelCount { get; init; }

    /// <summary>Pixels actually examined, which is the image area less any ignored rectangles.</summary>
    public required long ComparedPixelCount { get; init; }

    /// <summary>Pixels excluded by ignore rectangles, counted once even where rectangles overlap.</summary>
    public required long IgnoredPixelCount { get; init; }

    /// <summary>Bounding boxes around the changed areas, largest first.</summary>
    public required IReadOnlyList<CaptureRegion> ChangedRegions { get; init; }

    /// <summary>Human readable explanation, populated for failures that need more than a number.</summary>
    public string? Detail { get; init; }

    /// <summary>Changed pixels as a fraction of those compared. Zero when nothing was compared.</summary>
    public double ChangedPixelRatio =>
        ComparedPixelCount <= 0 ? 0 : (double)ChangedPixelCount / ComparedPixelCount;

    /// <summary>True only when the verdict is <see cref="ComparisonOutcome.Match"/>.</summary>
    public bool Passed => Outcome is ComparisonOutcome.Match;

    /// <summary>
    /// Builds the result used when the two images cannot be compared pixel by pixel because their
    /// dimensions differ.
    /// </summary>
    /// <param name="baselineWidth">Width of the approved image.</param>
    /// <param name="baselineHeight">Height of the approved image.</param>
    /// <param name="candidateWidth">Width of the fresh capture.</param>
    /// <param name="candidateHeight">Height of the fresh capture.</param>
    public static ComparisonResult ForSizeMismatch(
        int baselineWidth,
        int baselineHeight,
        int candidateWidth,
        int candidateHeight) =>
        new()
        {
            Outcome = ComparisonOutcome.SizeMismatch,
            ChangedPixelCount = 0,
            ComparedPixelCount = 0,
            IgnoredPixelCount = 0,
            ChangedRegions = [],
            Detail =
                $"Baseline is {baselineWidth}x{baselineHeight} but the capture is {candidateWidth}x{candidateHeight}.",
        };

    /// <summary>Renders a single line suitable for console output or a list row.</summary>
    public override string ToString() => Outcome switch
    {
        ComparisonOutcome.Match => "match",
        ComparisonOutcome.SizeMismatch => $"size mismatch. {Detail}",
        _ => $"{ChangedPixelCount} of {ComparedPixelCount} pixels changed in {ChangedRegions.Count} area(s)",
    };
}
