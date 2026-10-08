using OpenCvSharp;

namespace PixelDiff.Core.Imaging;

/// <summary>
/// Converts between in memory images and the PNG bytes kept on disk. Encoding is routed through
/// the byte array overloads rather than the file path ones because the OpenCV file helpers go
/// through a narrow character path on Windows and fail on paths outside the system code page.
/// </summary>
public static class ImageCodec
{
    private const string PngExtension = ".png";

    /// <summary>Encodes an image as PNG, which is lossless and therefore safe for a baseline.</summary>
    /// <param name="image">The image to encode.</param>
    /// <returns>PNG file content.</returns>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    /// <exception cref="InvalidOperationException">The encoder rejected the image.</exception>
    public static byte[] EncodePng(Mat image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Empty())
        {
            throw new ArgumentException("Cannot encode an empty image.", nameof(image));
        }

        if (!Cv2.ImEncode(PngExtension, image, out byte[] encoded))
        {
            throw new InvalidOperationException("The PNG encoder rejected the image.");
        }

        return encoded;
    }

    /// <summary>
    /// Decodes stored bytes into a three channel image. Normalising to BGR on the way in means the
    /// comparer never has to reason about an alpha channel, which window capture fills with values
    /// that carry no meaning.
    /// </summary>
    /// <param name="data">PNG or any other content the OpenCV decoders accept.</param>
    /// <returns>A three channel image the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentException">The bytes could not be decoded as an image.</exception>
    public static Mat DecodeBgr(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new ArgumentException("Cannot decode an empty byte array.", nameof(data));
        }

        Mat decoded = Cv2.ImDecode(data, ImreadModes.Color);
        if (decoded.Empty())
        {
            decoded.Dispose();
            throw new ArgumentException("The bytes are not a readable image.", nameof(data));
        }

        return decoded;
    }

    /// <summary>
    /// Returns a three channel copy of an image, converting from grayscale or from a format that
    /// carries alpha. The returned instance is always distinct from the input.
    /// </summary>
    /// <param name="image">The image to normalise.</param>
    /// <returns>A three channel image the caller owns and must dispose.</returns>
    public static Mat ToBgr(Mat image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var converted = new Mat();
        switch (image.Channels())
        {
            case 1:
                Cv2.CvtColor(image, converted, ColorConversionCodes.GRAY2BGR);
                break;

            case 4:
                Cv2.CvtColor(image, converted, ColorConversionCodes.BGRA2BGR);
                break;

            default:
                converted.Dispose();
                return image.Clone();
        }

        return converted;
    }
}
