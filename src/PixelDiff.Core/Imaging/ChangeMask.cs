using OpenCvSharp;
using PixelDiff.Core.Models;

namespace PixelDiff.Core.Imaging;

/// <summary>
/// Builds and interrogates the binary mask that marks which pixels moved between two captures.
/// The comparer and the diff renderer both work from the same mask so the number reported to the
/// user and the picture shown to the user can never disagree.
/// </summary>
public static class ChangeMask
{
    /// <summary>Channel count considered when collapsing a difference image.</summary>
    private const int ComparedChannelLimit = 3;

    /// <summary>
    /// Produces a single channel mask where 255 marks a changed pixel and 0 marks an unchanged one.
    /// A pixel counts as changed when any colour channel moved by strictly more than
    /// <paramref name="perChannelTolerance"/>.
    /// </summary>
    /// <param name="baseline">The approved image.</param>
    /// <param name="candidate">The freshly captured image, which must match the baseline size.</param>
    /// <param name="perChannelTolerance">
    /// Per channel delta absorbed without counting as a change, from 0 to 255. Small values in the
    /// region of 4 to 12 soak up font anti aliasing and video compression noise without hiding real
    /// layout movement.
    /// </param>
    /// <returns>A new <see cref="Mat"/> of type CV_8UC1 that the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentException">An image is empty or the two sizes differ.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance falls outside 0 to 255.</exception>
    public static Mat Build(Mat baseline, Mat candidate, int perChannelTolerance)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentOutOfRangeException.ThrowIfNegative(perChannelTolerance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(perChannelTolerance, 255);

        if (baseline.Empty() || candidate.Empty())
        {
            throw new ArgumentException("Cannot compare an empty image.", nameof(baseline));
        }

        if (baseline.Width != candidate.Width || baseline.Height != candidate.Height)
        {
            throw new ArgumentException(
                $"Image sizes differ: baseline is {baseline.Width}x{baseline.Height} and candidate is {candidate.Width}x{candidate.Height}.",
                nameof(candidate));
        }

        using var difference = new Mat();
        Cv2.Absdiff(baseline, candidate, difference);

        using Mat collapsed = CollapseChannels(difference);

        var mask = new Mat();
        Cv2.Threshold(collapsed, mask, perChannelTolerance, 255, ThresholdTypes.Binary);
        return mask;
    }

    /// <summary>
    /// Clears the ignored rectangles from <paramref name="mask"/> and reports how many pixels were
    /// excluded. Overlapping rectangles are counted once, so the returned figure can be subtracted
    /// from the total pixel count without going negative.
    /// </summary>
    /// <param name="mask">The mask to edit in place.</param>
    /// <param name="ignoreRegions">Rectangles to exclude, in image coordinates.</param>
    /// <returns>The number of distinct pixels now excluded from comparison.</returns>
    public static long Suppress(Mat mask, IReadOnlyList<CaptureRegion> ignoreRegions)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(ignoreRegions);

        if (ignoreRegions.Count == 0)
        {
            return 0;
        }

        var imageBounds = new CaptureRegion(0, 0, mask.Width, mask.Height);
        using var ignored = new Mat(mask.Size(), MatType.CV_8UC1, Scalar.Black);

        foreach (CaptureRegion region in ignoreRegions)
        {
            CaptureRegion visible = region.IntersectWith(imageBounds);
            if (visible.IsEmpty)
            {
                continue;
            }

            Cv2.Rectangle(ignored, ToRect(visible), Scalar.White, thickness: -1);
        }

        long suppressed = Cv2.CountNonZero(ignored);
        if (suppressed > 0)
        {
            mask.SetTo(Scalar.Black, ignored);
        }

        return suppressed;
    }

    /// <summary>
    /// Groups the changed pixels into bounding rectangles so a reviewer can see where to look
    /// instead of hunting across the whole image. Neighbouring specks are dilated together first,
    /// which turns a changed line of text into one box rather than one box per glyph.
    /// </summary>
    /// <param name="mask">A mask produced by <see cref="Build"/>.</param>
    /// <param name="minimumArea">Boxes smaller than this pixel area are discarded as noise.</param>
    /// <returns>Rectangles ordered largest first.</returns>
    public static IReadOnlyList<CaptureRegion> FindRegions(Mat mask, int minimumArea)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumArea);

        using var grouped = new Mat();
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        Cv2.Dilate(mask, grouped, kernel);

        Cv2.FindContours(
            grouped,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var regions = new List<CaptureRegion>(contours.Length);
        var imageBounds = new CaptureRegion(0, 0, mask.Width, mask.Height);

        foreach (Point[] contour in contours)
        {
            Rect box = Cv2.BoundingRect(contour);
            CaptureRegion region = new CaptureRegion(box.X, box.Y, box.Width, box.Height)
                .IntersectWith(imageBounds);

            if (!region.IsEmpty && region.Area >= minimumArea)
            {
                regions.Add(region);
            }
        }

        regions.Sort(static (left, right) => right.Area.CompareTo(left.Area));
        return regions;
    }

    /// <summary>
    /// Reduces a multi channel difference image to one channel by keeping the largest delta at each
    /// pixel. Converting to grayscale instead would average the channels, which can hide a colour
    /// change that leaves overall brightness untouched. A fourth channel is skipped because the
    /// alpha produced by window capture is not meaningful.
    /// </summary>
    private static Mat CollapseChannels(Mat difference)
    {
        if (difference.Channels() == 1)
        {
            return difference.Clone();
        }

        Mat[] channels = Cv2.Split(difference);
        try
        {
            int limit = Math.Min(channels.Length, ComparedChannelLimit);
            Mat accumulator = channels[0].Clone();

            try
            {
                for (int index = 1; index < limit; index++)
                {
                    Cv2.Max(accumulator, channels[index], accumulator);
                }

                return accumulator;
            }
            catch
            {
                // The accumulator is a full size image buffer, so it is released rather than left
                // to the finaliser if a later channel cannot be folded in.
                accumulator.Dispose();
                throw;
            }
        }
        finally
        {
            foreach (Mat channel in channels)
            {
                channel.Dispose();
            }
        }
    }

    /// <summary>Converts a region to the rectangle type the OpenCV bindings expect.</summary>
    internal static Rect ToRect(CaptureRegion region) => new(region.X, region.Y, region.Width, region.Height);
}
