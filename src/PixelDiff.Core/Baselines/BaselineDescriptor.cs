using PixelDiff.Core.Comparison;
using PixelDiff.Core.Models;

namespace PixelDiff.Core.Baselines;

/// <summary>
/// The metadata stored beside a baseline image. Keeping the capture target and the tolerances with
/// the image is what allows an unattended run to reacquire and judge the same view without being
/// configured separately.
/// </summary>
public sealed record BaselineDescriptor
{
    /// <summary>Identifier shared by the image file and this descriptor.</summary>
    public required string Name { get; init; }

    /// <summary>Where the image came from and how to acquire it again.</summary>
    public required CaptureTarget Target { get; init; }

    /// <summary>Width of the stored image in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height of the stored image in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>When the baseline was approved, in UTC.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Tolerances applied when judging a fresh capture against this baseline.</summary>
    public ComparisonOptions Options { get; init; } = new();

    /// <summary>Free text left by whoever approved the baseline, such as a ticket reference.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Throws when the descriptor is internally inconsistent, which happens when a file has been
    /// hand edited or written by an older version of the tool.
    /// </summary>
    /// <exception cref="InvalidOperationException">The descriptor cannot be used.</exception>
    public void Validate()
    {
        BaselineName.Validate(Name, nameof(Name));

        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidOperationException(
                $"Baseline '{Name}' records an impossible size of {Width}x{Height}.");
        }

        Target.Validate();
        Options.Validate();

        foreach (CaptureRegion region in Options.IgnoreRegions)
        {
            if (region.IntersectWith(new CaptureRegion(0, 0, Width, Height)).IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Baseline '{Name}' has an ignore region at {region} that falls outside the image.");
            }
        }
    }
}
