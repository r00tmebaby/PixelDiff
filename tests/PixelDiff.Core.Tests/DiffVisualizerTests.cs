using OpenCvSharp;
using PixelDiff.Core.Comparison;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class DiffVisualizerTests
{
    private static readonly CaptureRegion Block = new(40, 40, 10, 10);

    [Fact]
    public void RenderReturnsAThreeChannelImageTheSizeOfTheCandidate()
    {
        using Mat baseline = TestImages.Grey(80, 60);
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, new CaptureRegion(10, 10, 8, 8), 0, 0, 0);

        using Mat rendered = DiffVisualizer.Render(baseline, candidate, new ComparisonOptions());

        Assert.Equal(3, rendered.Channels());
        Assert.Equal(candidate.Size(), rendered.Size());
    }

    [Fact]
    public void RenderPaintsChangedPixelsInTheConfiguredColour()
    {
        using Mat baseline = TestImages.Grey(100, 100, 200);
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);
        var appearance = new DiffRenderOptions { ChangeColour = new Scalar(1, 2, 3) };

        using Mat rendered = DiffVisualizer.Render(baseline, candidate, new ComparisonOptions(), appearance);

        Assert.Equal(((byte)1, (byte)2, (byte)3), TestImages.PixelAt(rendered, 45, 45));
    }

    [Fact]
    public void RenderDimsUnchangedPixelsInsteadOfLeavingThemUntouched()
    {
        using Mat baseline = TestImages.Grey(100, 100, 200);
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);
        var appearance = new DiffRenderOptions { BackgroundBrightness = 0.5 };

        using Mat rendered = DiffVisualizer.Render(baseline, candidate, new ComparisonOptions(), appearance);

        Assert.Equal(((byte)100, (byte)100, (byte)100), TestImages.PixelAt(rendered, 5, 5));
    }

    [Fact]
    public void RenderDoesNotModifyTheCandidate()
    {
        using Mat baseline = TestImages.Grey(100, 100, 200);
        using Mat candidate = baseline.Clone();
        TestImages.Paint(candidate, Block, 0, 0, 0);

        using Mat rendered = DiffVisualizer.Render(baseline, candidate, new ComparisonOptions());

        Assert.NotSame(candidate, rendered);
        Assert.Equal(((byte)200, (byte)200, (byte)200), TestImages.PixelAt(candidate, 5, 5));
        Assert.Equal(((byte)0, (byte)0, (byte)0), TestImages.PixelAt(candidate, 45, 45));
    }

    [Fact]
    public void RenderThrowsWhenTheSizesDiffer()
    {
        using Mat baseline = TestImages.Grey(100, 100);
        using Mat candidate = TestImages.Grey(100, 90);

        Assert.Throws<ArgumentException>(() => DiffVisualizer.Render(baseline, candidate, new ComparisonOptions()));
    }
}
