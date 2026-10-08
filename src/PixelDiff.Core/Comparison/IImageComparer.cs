using OpenCvSharp;

namespace PixelDiff.Core.Comparison;

/// <summary>
/// Decides whether a fresh capture still matches its approved baseline. Kept as an interface so a
/// perceptual or structural comparer can be dropped in later without touching the callers.
/// </summary>
public interface IImageComparer
{
    /// <summary>Measures the difference between an approved image and a fresh one.</summary>
    /// <param name="baseline">The approved image.</param>
    /// <param name="candidate">The fresh capture.</param>
    /// <param name="options">Tolerances to apply.</param>
    /// <returns>The measurement, including a verdict and the changed regions.</returns>
    ComparisonResult Compare(Mat baseline, Mat candidate, ComparisonOptions options);
}
