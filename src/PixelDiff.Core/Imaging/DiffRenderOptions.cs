using OpenCvSharp;

namespace PixelDiff.Core.Imaging;

/// <summary>
/// Appearance of the review image. Colours are given in the blue, green, red order that OpenCV
/// uses throughout, not in the red, green, blue order used by most design tools.
/// </summary>
public sealed record DiffRenderOptions
{
    /// <summary>
    /// Brightness multiplier applied to the unchanged part of the picture, from 0 to 1. Dimming the
    /// background is what makes a handful of changed pixels findable in a full screen capture.
    /// </summary>
    public double BackgroundBrightness { get; init; } = 0.35;

    /// <summary>Colour painted over every changed pixel. Magenta by default.</summary>
    public Scalar ChangeColour { get; init; } = new(255, 0, 255);

    /// <summary>Colour of the box drawn around each changed area. Amber by default.</summary>
    public Scalar RegionOutlineColour { get; init; } = new(0, 190, 255);

    /// <summary>Colour of the box drawn around each ignored area. Steel blue by default.</summary>
    public Scalar IgnoreOutlineColour { get; init; } = new(200, 140, 60);

    /// <summary>Line width in pixels for the boxes drawn around changed areas.</summary>
    public int OutlineThickness { get; init; } = 2;

    /// <summary>Appearance used when a caller does not supply its own.</summary>
    public static DiffRenderOptions Default { get; } = new();

    /// <summary>Throws when a value would produce an unusable image.</summary>
    /// <exception cref="InvalidOperationException">A value is outside its valid range.</exception>
    public void Validate()
    {
        if (BackgroundBrightness is < 0 or > 1 || double.IsNaN(BackgroundBrightness))
        {
            throw new InvalidOperationException(
                $"{nameof(BackgroundBrightness)} must be between 0 and 1 but was {BackgroundBrightness}.");
        }

        if (OutlineThickness < 1)
        {
            throw new InvalidOperationException(
                $"{nameof(OutlineThickness)} must be at least 1 but was {OutlineThickness}.");
        }
    }
}
