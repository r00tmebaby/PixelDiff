using OpenCvSharp;
using PixelDiff.Core.Imaging;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class ImageCodecTests
{
    [Fact]
    public void EncodePngThenDecodeBgrRoundTripsPixelValuesExactly()
    {
        using Mat source = TestImages.Grey(32, 24, 40);
        TestImages.Paint(source, new CaptureRegion(4, 4, 8, 8), 1, 2, 3);
        TestImages.Paint(source, new CaptureRegion(16, 10, 6, 6), 250, 251, 252);

        using Mat decoded = ImageCodec.DecodeBgr(ImageCodec.EncodePng(source));

        using var difference = new Mat();
        Cv2.Absdiff(source, decoded, difference);
        Assert.Equal(source.Size(), decoded.Size());
        Assert.Equal(0, Cv2.CountNonZero(difference.Reshape(1)));
    }

    [Fact]
    public void DecodeBgrReturnsThreeChannelsForAGrayscaleSource()
    {
        using var grey = new Mat(10, 10, MatType.CV_8UC1, new Scalar(77));

        using Mat decoded = ImageCodec.DecodeBgr(ImageCodec.EncodePng(grey));

        Assert.Equal(3, decoded.Channels());
    }

    [Fact]
    public void DecodeBgrDropsTheAlphaChannel()
    {
        using var bgra = new Mat(10, 10, MatType.CV_8UC4, new Scalar(10, 20, 30, 40));

        using Mat decoded = ImageCodec.DecodeBgr(ImageCodec.EncodePng(bgra));

        Assert.Equal(3, decoded.Channels());
        Assert.Equal(((byte)10, (byte)20, (byte)30), TestImages.PixelAt(decoded, 0, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void ToBgrReturnsAThreeChannelCopyDistinctFromTheInput(int channels)
    {
        using var source = new Mat(8, 8, MatType.CV_8UC(channels), Scalar.All(50));

        using Mat converted = ImageCodec.ToBgr(source);

        Assert.Equal(3, converted.Channels());
        Assert.NotSame(source, converted);
        Assert.NotEqual(source.Data, converted.Data);
    }

    [Fact]
    public void EncodePngRejectsAnEmptyImage()
    {
        using var empty = new Mat();

        Assert.Throws<ArgumentException>(() => ImageCodec.EncodePng(empty));
    }

    [Fact]
    public void DecodeBgrRejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => ImageCodec.DecodeBgr([]));
    }

    [Fact]
    public void DecodeBgrRejectsBytesThatAreNotAnImage()
    {
        Assert.Throws<ArgumentException>(() => ImageCodec.DecodeBgr([1, 2, 3, 4, 5]));
    }
}
