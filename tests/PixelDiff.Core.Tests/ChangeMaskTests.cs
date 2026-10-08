using OpenCvSharp;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class ChangeMaskTests
{
    [Fact]
    public void BuildMarksExactlyTheChangedPixelsInASingleChannelMask()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();
        var block = new CaptureRegion(10, 10, 5, 4);
        TestImages.Paint(candidate, block, 0, 0, 0);

        using Mat mask = ChangeMask.Build(baseline, candidate, perChannelTolerance: 0);

        Assert.Equal(1, mask.Channels());
        Assert.Equal(100, mask.Width);
        Assert.Equal(100, mask.Height);
        Assert.Equal(block.Area, Cv2.CountNonZero(mask));
        Assert.Equal(255, mask.At<byte>(10, 10));
        Assert.Equal(255, mask.At<byte>(13, 14));
        Assert.Equal(0, mask.At<byte>(14, 10));
        Assert.Equal(0, mask.At<byte>(10, 15));
    }

    [Fact]
    public void BuildRejectsImagesOfDifferentSizes()
    {
        using Mat baseline = TestImages.Grey(10, 10);
        using Mat candidate = TestImages.Grey(10, 11);

        Assert.Throws<ArgumentException>(() => ChangeMask.Build(baseline, candidate, 0));
    }

    [Fact]
    public void BuildRejectsAToleranceOutsideTheByteRange()
    {
        using Mat image = TestImages.Grey(10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeMask.Build(image, image, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeMask.Build(image, image, 256));
    }

    [Fact]
    public void SuppressZeroesTheIgnoredRectangle()
    {
        using var mask = new Mat(50, 50, MatType.CV_8UC1, Scalar.White);

        long suppressed = ChangeMask.Suppress(mask, [new CaptureRegion(10, 10, 10, 10)]);

        Assert.Equal(100, suppressed);
        Assert.Equal(2_500 - 100, Cv2.CountNonZero(mask));
        Assert.Equal(0, mask.At<byte>(10, 10));
        Assert.Equal(0, mask.At<byte>(19, 19));
        Assert.Equal(255, mask.At<byte>(20, 20));
    }

    [Fact]
    public void SuppressClipsARectangleThatHangsOffTheEdge()
    {
        using var mask = new Mat(20, 20, MatType.CV_8UC1, Scalar.White);

        // Only the 5x5 corner of this rectangle lies inside the image.
        long suppressed = ChangeMask.Suppress(mask, [new CaptureRegion(15, 15, 10, 10)]);

        Assert.Equal(25, suppressed);
        Assert.Equal(0, mask.At<byte>(19, 19));
        Assert.Equal(255, mask.At<byte>(14, 14));
    }

    [Fact]
    public void SuppressReturnsTheUnionCountForOverlappingRectangles()
    {
        using var mask = new Mat(30, 30, MatType.CV_8UC1, Scalar.White);

        long suppressed = ChangeMask.Suppress(mask, [new CaptureRegion(0, 0, 10, 10), new CaptureRegion(5, 5, 10, 10)]);

        Assert.Equal(175, suppressed);
    }

    [Fact]
    public void SuppressIgnoresARectangleEntirelyOutsideTheImage()
    {
        using var mask = new Mat(20, 20, MatType.CV_8UC1, Scalar.White);

        long suppressed = ChangeMask.Suppress(mask, [new CaptureRegion(100, 100, 10, 10)]);

        Assert.Equal(0, suppressed);
        Assert.Equal(400, Cv2.CountNonZero(mask));
    }

    [Fact]
    public void FindRegionsGroupsAdjacentSpecksIntoOneBox()
    {
        // Two single pixels three columns apart. Each grows by two pixels in every direction when
        // dilated, so their neighbourhoods overlap and merge.
        using var mask = new Mat(100, 100, MatType.CV_8UC1, Scalar.Black);
        mask.Set(10, 10, (byte)255);
        mask.Set(10, 13, (byte)255);

        IReadOnlyList<CaptureRegion> regions = ChangeMask.FindRegions(mask, minimumArea: 0);

        CaptureRegion region = Assert.Single(regions);
        Assert.True(region.X <= 10 && region.Right > 13);
    }

    [Fact]
    public void FindRegionsKeepsDistantSpecksSeparate()
    {
        using var mask = new Mat(100, 100, MatType.CV_8UC1, Scalar.Black);
        mask.Set(10, 10, (byte)255);
        mask.Set(80, 80, (byte)255);

        Assert.Equal(2, ChangeMask.FindRegions(mask, minimumArea: 0).Count);
    }

    [Fact]
    public void FindRegionsDiscardsABoxBelowTheMinimumArea()
    {
        // A lone pixel dilates to a 5x5 box of 25 pixels.
        using var mask = new Mat(50, 50, MatType.CV_8UC1, Scalar.Black);
        mask.Set(25, 25, (byte)255);

        Assert.Single(ChangeMask.FindRegions(mask, minimumArea: 25));
        Assert.Empty(ChangeMask.FindRegions(mask, minimumArea: 26));
    }

    [Fact]
    public void FindRegionsOrdersBoxesLargestFirst()
    {
        using var mask = new Mat(100, 100, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mask, new Rect(5, 5, 4, 4), Scalar.White, thickness: -1);
        Cv2.Rectangle(mask, new Rect(50, 50, 30, 30), Scalar.White, thickness: -1);
        Cv2.Rectangle(mask, new Rect(10, 70, 12, 12), Scalar.White, thickness: -1);

        IReadOnlyList<CaptureRegion> regions = ChangeMask.FindRegions(mask, minimumArea: 0);

        Assert.Equal(3, regions.Count);
        Assert.True(regions[0].Area >= regions[1].Area);
        Assert.True(regions[1].Area >= regions[2].Area);
        Assert.True(regions[0].X >= 48);
    }
}
