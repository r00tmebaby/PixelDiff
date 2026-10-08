using OpenCvSharp;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;

namespace PixelDiff.Core.Comparison;

/// <summary>
/// Compares two captures pixel by pixel. This is the right strategy for deterministic user
/// interface rendering, where any difference is either a real change or noise small enough for the
/// per channel tolerance to absorb. Photographic or video content needs a perceptual comparer
/// instead, which is why callers depend on <see cref="IImageComparer"/>.
/// </summary>
public sealed class PixelImageComparer : IImageComparer
{
    /// <inheritdoc />
    /// <exception cref="ArgumentException">An image is empty.</exception>
    /// <exception cref="InvalidOperationException">The options contain an invalid tolerance.</exception>
    public ComparisonResult Compare(Mat baseline, Mat candidate, ComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (baseline.Empty() || candidate.Empty())
        {
            throw new ArgumentException("Cannot compare an empty image.", nameof(baseline));
        }

        if (baseline.Width != candidate.Width || baseline.Height != candidate.Height)
        {
            return ComparisonResult.ForSizeMismatch(
                baseline.Width,
                baseline.Height,
                candidate.Width,
                candidate.Height);
        }

        using Mat mask = ChangeMask.Build(baseline, candidate, options.PerChannelTolerance);

        long ignored = ChangeMask.Suppress(mask, options.IgnoreRegions);
        long changed = Cv2.CountNonZero(mask);
        long compared = ((long)mask.Width * mask.Height) - ignored;

        IReadOnlyList<CaptureRegion> regions = changed > 0
            ? ChangeMask.FindRegions(mask, options.MinimumRegionArea)
            : [];

        double ratio = compared <= 0 ? 0 : (double)changed / compared;

        return new ComparisonResult
        {
            Outcome = ratio <= options.MaxChangedPixelRatio
                ? ComparisonOutcome.Match
                : ComparisonOutcome.Differs,
            ChangedPixelCount = changed,
            ComparedPixelCount = compared,
            IgnoredPixelCount = ignored,
            ChangedRegions = regions,
            Detail = compared <= 0 ? "Every pixel was excluded by an ignore region." : null,
        };
    }
}
