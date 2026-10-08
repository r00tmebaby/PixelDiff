using Avalonia.Media.Imaging;
using OpenCvSharp;
using PixelDiff.Core.Imaging;

namespace PixelDiff.App.Services;

/// <summary>
/// Turns OpenCV images into bitmaps the interface can draw. The conversion goes through a PNG
/// encode rather than copying pixels, which trades some CPU time for not having to match pixel
/// formats and row strides by hand. That is the right trade for a review tool, which redraws on a
/// user action rather than on every frame.
/// </summary>
public static class MatImageSource
{
    /// <summary>Creates a bitmap that holds its own copy of the image data.</summary>
    /// <param name="image">The image to convert. It is not retained or disposed.</param>
    /// <returns>A bitmap the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    public static Bitmap ToBitmap(Mat image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Empty())
        {
            throw new ArgumentException("Cannot display an empty image.", nameof(image));
        }

        byte[] png = ImageCodec.EncodePng(image);
        using var stream = new MemoryStream(png, writable: false);

        // The bitmap decodes eagerly, so the stream may be released as soon as the constructor returns.
        return new Bitmap(stream);
    }
}
