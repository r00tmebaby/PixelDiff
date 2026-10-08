using OpenCvSharp;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class PixelImageComparerTests
{
    private static readonly CaptureRegion Block = new(20, 20, 10, 10);

    private readonly PixelImageComparer _comparer = new();

    [Fact]
    public void IdenticalImagesMatchWithZeroChangedPixels()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions());

        Assert.Equal(ComparisonOutcome.Match, result.Outcome);
        Assert.Equal(0, result.ChangedPixelCount);
        Assert.Equal(10_000, result.ComparedPixelCount);
        Assert.Empty(result.ChangedRegions);
        Assert.True(result.Passed);
    }

    [Fact]
    public void ASingleChangedBlockDiffersAndIsReportedAsOneRegion()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions());

        Assert.Equal(ComparisonOutcome.Differs, result.Outcome);
        Assert.Equal(Block.Area, result.ChangedPixelCount);
        CaptureRegion region = Assert.Single(result.ChangedRegions);
        Assert.Equal(Block, Block.IntersectWith(region));
    }

    [Fact]
    public void ADeltaEqualToTheToleranceCountsAsUnchanged()
    {
        using Mat baseline = TestImages.Grey(level: 100);
        using Mat candidate = TestImages.Grey(level: 108);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions { PerChannelTolerance = 8 });

        Assert.Equal(ComparisonOutcome.Match, result.Outcome);
        Assert.Equal(0, result.ChangedPixelCount);
    }

    [Fact]
    public void ADeltaOneAboveTheToleranceCountsAsChanged()
    {
        using Mat baseline = TestImages.Grey(level: 100);
        using Mat candidate = TestImages.Grey(level: 109);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions { PerChannelTolerance = 8 });

        Assert.Equal(ComparisonOutcome.Differs, result.Outcome);
        Assert.Equal(10_000, result.ChangedPixelCount);
    }

    [Fact]
    public void AChangeConfinedToAnIgnoreRegionMatches()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);
        var options = new ComparisonOptions { IgnoreRegions = [Block] };

        ComparisonResult result = _comparer.Compare(baseline, candidate, options);

        Assert.Equal(ComparisonOutcome.Match, result.Outcome);
        Assert.Equal(0, result.ChangedPixelCount);
        Assert.Equal(Block.Area, result.IgnoredPixelCount);
        Assert.Equal(10_000 - Block.Area, result.ComparedPixelCount);
    }

    [Fact]
    public void OverlappingIgnoreRegionsCountSharedPixelsOnce()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();

        // Two 10x10 squares sharing a 5x5 corner cover 100 + 100 - 25 pixels.
        var options = new ComparisonOptions
        {
            IgnoreRegions = [new CaptureRegion(0, 0, 10, 10), new CaptureRegion(5, 5, 10, 10)],
        };

        ComparisonResult result = _comparer.Compare(baseline, candidate, options);

        Assert.Equal(175, result.IgnoredPixelCount);
    }

    [Fact]
    public void DifferentSizesProduceASizeMismatchWithoutPixelComparison()
    {
        using Mat baseline = TestImages.Grey(100, 100);
        using Mat candidate = TestImages.Grey(100, 120);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions());

        Assert.Equal(ComparisonOutcome.SizeMismatch, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
        Assert.Equal(0, result.ChangedPixelCount);
        Assert.Equal(0, result.ComparedPixelCount);
        Assert.False(result.Passed);
    }

    [Fact]
    public void AChangeJustUnderTheAllowedRatioPasses()
    {
        // The block is 100 of 10000 pixels, a ratio of 0.01.
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions { MaxChangedPixelRatio = 0.011 });

        Assert.Equal(ComparisonOutcome.Match, result.Outcome);
        Assert.Equal(Block.Area, result.ChangedPixelCount);
    }

    [Fact]
    public void AChangeJustOverTheAllowedRatioFails()
    {
        using Mat baseline = TestImages.Grey();
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions { MaxChangedPixelRatio = 0.009 });

        Assert.Equal(ComparisonOutcome.Differs, result.Outcome);
    }

    [Fact]
    public void AColourChangeThatLeavesLuminanceUnchangedIsDetected()
    {
        // Luminance is 0.299 R + 0.587 G + 0.114 B. Raising red by 59 while lowering green by 30
        // moves the weighted sum by roughly zero, so a grayscale comparison would see no change.
        using Mat baseline = TestImages.Solid(50, 50, blue: 100, green: 100, red: 100);
        using Mat candidate = TestImages.Solid(50, 50, blue: 100, green: 70, red: 159);

        using var baselineGrey = new Mat();
        using var candidateGrey = new Mat();
        Cv2.CvtColor(baseline, baselineGrey, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(candidate, candidateGrey, ColorConversionCodes.BGR2GRAY);
        Assert.True(Math.Abs(baselineGrey.At<byte>(0, 0) - candidateGrey.At<byte>(0, 0)) <= 1);

        ComparisonResult result = _comparer.Compare(baseline, candidate, new ComparisonOptions());

        Assert.Equal(ComparisonOutcome.Differs, result.Outcome);
        Assert.Equal(2_500, result.ChangedPixelCount);
    }

    [Fact]
    public void EmptyImagesThrowArgumentException()
    {
        using var empty = new Mat();
        using Mat image = TestImages.Grey();

        Assert.Throws<ArgumentException>(() => _comparer.Compare(empty, image, new ComparisonOptions()));
        Assert.Throws<ArgumentException>(() => _comparer.Compare(image, empty, new ComparisonOptions()));
    }

    [Theory]
    [InlineData(-1, 0.0)]
    [InlineData(256, 0.0)]
    [InlineData(8, -0.1)]
    [InlineData(8, 1.1)]
    public void InvalidOptionsThrowInvalidOperationException(int tolerance, double ratio)
    {
        using Mat image = TestImages.Grey();
        var options = new ComparisonOptions { PerChannelTolerance = tolerance, MaxChangedPixelRatio = ratio };

        Assert.Throws<InvalidOperationException>(() => _comparer.Compare(image, image, options));
    }
}
