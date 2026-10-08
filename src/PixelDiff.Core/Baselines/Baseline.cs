using OpenCvSharp;

namespace PixelDiff.Core.Baselines;

/// <summary>
/// An approved image together with its metadata. Owns the image, so callers dispose the baseline
/// rather than the <see cref="Mat"/> they read from it.
/// </summary>
public sealed class Baseline : IDisposable
{
    private bool _disposed;

    /// <summary>Creates a baseline from an image and its metadata.</summary>
    /// <param name="image">The approved image. Ownership transfers to this instance.</param>
    /// <param name="descriptor">Metadata describing the image.</param>
    /// <exception cref="ArgumentException">The image is empty or its size contradicts the descriptor.</exception>
    public Baseline(Mat image, BaselineDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (image.Empty())
        {
            throw new ArgumentException("A baseline image cannot be empty.", nameof(image));
        }

        if (image.Width != descriptor.Width || image.Height != descriptor.Height)
        {
            throw new ArgumentException(
                $"Descriptor records {descriptor.Width}x{descriptor.Height} but the image is {image.Width}x{image.Height}.",
                nameof(descriptor));
        }

        Image = image;
        Descriptor = descriptor;
    }

    /// <summary>The approved image, owned by this instance.</summary>
    public Mat Image { get; }

    /// <summary>Metadata describing the image.</summary>
    public BaselineDescriptor Descriptor { get; }

    /// <summary>Releases the image.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Image.Dispose();
        _disposed = true;
    }
}
