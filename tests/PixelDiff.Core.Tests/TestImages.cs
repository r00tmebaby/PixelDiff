using OpenCvSharp;
using PixelDiff.Core.Models;

namespace PixelDiff.Core.Tests;

/// <summary>Builders for the small synthetic images the tests compare.</summary>
internal static class TestImages
{
    /// <summary>Creates a three channel image filled with one colour given in blue, green, red order.</summary>
    public static Mat Solid(int width, int height, byte blue, byte green, byte red) =>
        new(height, width, MatType.CV_8UC3, new Scalar(blue, green, red));

    /// <summary>Creates a grey image, the neutral background most tests start from.</summary>
    public static Mat Grey(int width = 100, int height = 100, byte level = 128) =>
        Solid(width, height, level, level, level);

    /// <summary>Paints a filled rectangle into an image in place.</summary>
    public static void Paint(Mat image, CaptureRegion region, byte blue, byte green, byte red) =>
        Cv2.Rectangle(
            image,
            new Rect(region.X, region.Y, region.Width, region.Height),
            new Scalar(blue, green, red),
            thickness: -1);

    /// <summary>Reads one pixel as blue, green, red.</summary>
    public static (byte Blue, byte Green, byte Red) PixelAt(Mat image, int x, int y)
    {
        Vec3b pixel = image.At<Vec3b>(y, x);
        return (pixel.Item0, pixel.Item1, pixel.Item2);
    }
}
