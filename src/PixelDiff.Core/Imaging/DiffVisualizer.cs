using OpenCvSharp;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;

namespace PixelDiff.Core.Imaging;

/// <summary>
/// Renders the picture a reviewer looks at when a comparison fails. The mask is rebuilt from the
/// same inputs and the same tolerances the comparer used, so the highlighted pixels always agree
/// with the reported count rather than drifting from it.
/// </summary>
public static class DiffVisualizer
{
    /// <summary>
    /// Produces a review image showing the fresh capture dimmed, every changed pixel painted in the
    /// change colour, and a box around each changed area.
    /// </summary>
    /// <param name="baseline">The approved image.</param>
    /// <param name="candidate">The fresh capture, which must match the baseline size.</param>
    /// <param name="comparison">The tolerances used for the comparison being illustrated.</param>
    /// <param name="render">Appearance overrides, or null for the defaults.</param>
    /// <returns>A three channel image the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentException">The images are empty or differently sized.</exception>
    public static Mat Render(
        Mat baseline,
        Mat candidate,
        ComparisonOptions comparison,
        DiffRenderOptions? render = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(comparison);

        comparison.Validate();
        DiffRenderOptions appearance = render ?? DiffRenderOptions.Default;
        appearance.Validate();

        using Mat mask = ChangeMask.Build(baseline, candidate, comparison.PerChannelTolerance);
        ChangeMask.Suppress(mask, comparison.IgnoreRegions);

        IReadOnlyList<CaptureRegion> regions = ChangeMask.FindRegions(mask, comparison.MinimumRegionArea);
        return Compose(candidate, mask, regions, comparison.IgnoreRegions, appearance);
    }

    /// <summary>
    /// Builds the review image from an already computed mask. Exposed separately so a caller that
    /// has just run a comparison can avoid rebuilding the mask a second time.
    /// </summary>
    /// <param name="candidate">The fresh capture used as the background.</param>
    /// <param name="changeMask">Single channel mask where 255 marks a changed pixel.</param>
    /// <param name="changedRegions">Boxes to draw around the changed areas.</param>
    /// <param name="ignoreRegions">Boxes to draw around the excluded areas.</param>
    /// <param name="render">Appearance overrides, or null for the defaults.</param>
    /// <returns>A three channel image the caller owns and must dispose.</returns>
    public static Mat Compose(
        Mat candidate,
        Mat changeMask,
        IReadOnlyList<CaptureRegion> changedRegions,
        IReadOnlyList<CaptureRegion> ignoreRegions,
        DiffRenderOptions? render = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(changeMask);
        ArgumentNullException.ThrowIfNull(changedRegions);
        ArgumentNullException.ThrowIfNull(ignoreRegions);

        DiffRenderOptions appearance = render ?? DiffRenderOptions.Default;
        appearance.Validate();

        using var dimmed = new Mat();
        candidate.ConvertTo(dimmed, candidate.Type(), appearance.BackgroundBrightness);

        Mat canvas = ImageCodec.ToBgr(dimmed);

        try
        {
            canvas.SetTo(appearance.ChangeColour, changeMask);

            foreach (CaptureRegion region in ignoreRegions)
            {
                Cv2.Rectangle(canvas, ChangeMask.ToRect(region), appearance.IgnoreOutlineColour, thickness: 1);
            }

            foreach (CaptureRegion region in changedRegions)
            {
                Cv2.Rectangle(
                    canvas,
                    ChangeMask.ToRect(Expand(region, appearance.OutlineThickness, canvas.Width, canvas.Height)),
                    appearance.RegionOutlineColour,
                    appearance.OutlineThickness);
            }

            return canvas;
        }
        catch
        {
            canvas.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Grows a box outwards so its outline sits beside the changed pixels instead of on top of
    /// them, then clips the result back inside the image.
    /// </summary>
    private static CaptureRegion Expand(CaptureRegion region, int margin, int imageWidth, int imageHeight) =>
        CaptureRegion
            .FromEdges(region.X - margin, region.Y - margin, region.Right + margin, region.Bottom + margin)
            .IntersectWith(new CaptureRegion(0, 0, imageWidth, imageHeight));
}
