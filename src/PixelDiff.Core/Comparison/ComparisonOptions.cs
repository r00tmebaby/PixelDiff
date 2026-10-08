using PixelDiff.Core.Models;

namespace PixelDiff.Core.Comparison;

/// <summary>
/// Tolerances applied to one comparison. Each baseline carries its own copy so a page with a
/// live clock on it can be looser than the rest of the suite without weakening everything.
/// </summary>
public sealed record ComparisonOptions
{
    /// <summary>
    /// Per channel delta absorbed before a pixel counts as changed, from 0 to 255. The default
    /// soaks up font anti aliasing differences while still catching a colour or layout change.
    /// </summary>
    public int PerChannelTolerance { get; init; } = 8;

    /// <summary>
    /// Fraction of compared pixels allowed to change before the comparison fails, from 0 to 1.
    /// Zero demands an exact match once <see cref="PerChannelTolerance"/> has been applied, which
    /// is the right default for a regression check. Raise it for content that reflows slightly
    /// between runs.
    /// </summary>
    public double MaxChangedPixelRatio { get; init; }

    /// <summary>
    /// Rectangles excluded from comparison, in image coordinates relative to the top left corner
    /// of the baseline. Use these for timestamps, counters and anything else that legitimately
    /// differs on every run.
    /// </summary>
    public IReadOnlyList<CaptureRegion> IgnoreRegions { get; init; } = [];

    /// <summary>
    /// Smallest pixel area a changed group must cover to be reported as a region. This affects the
    /// boxes drawn for a reviewer, never the pass or fail decision.
    /// </summary>
    public int MinimumRegionArea { get; init; } = 16;

    /// <summary>Tolerances used when a baseline does not specify its own.</summary>
    public static ComparisonOptions Default { get; } = new();

    /// <summary>Throws when any tolerance is outside its valid range.</summary>
    /// <exception cref="InvalidOperationException">A value cannot be used for a comparison.</exception>
    public void Validate()
    {
        if (PerChannelTolerance is < 0 or > 255)
        {
            throw new InvalidOperationException(
                $"{nameof(PerChannelTolerance)} must be between 0 and 255 but was {PerChannelTolerance}.");
        }

        if (MaxChangedPixelRatio is < 0 or > 1 || double.IsNaN(MaxChangedPixelRatio))
        {
            throw new InvalidOperationException(
                $"{nameof(MaxChangedPixelRatio)} must be between 0 and 1 but was {MaxChangedPixelRatio}.");
        }

        if (MinimumRegionArea < 0)
        {
            throw new InvalidOperationException(
                $"{nameof(MinimumRegionArea)} cannot be negative but was {MinimumRegionArea}.");
        }

        foreach (CaptureRegion region in IgnoreRegions)
        {
            if (region.IsEmpty)
            {
                throw new InvalidOperationException("An ignore region covers no pixels.");
            }
        }
    }
}
