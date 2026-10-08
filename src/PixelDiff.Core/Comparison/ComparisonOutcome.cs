namespace PixelDiff.Core.Comparison;

/// <summary>The verdict for a single baseline against a single fresh capture.</summary>
public enum ComparisonOutcome
{
    /// <summary>The images agree within the configured tolerances.</summary>
    Match = 0,

    /// <summary>The images are the same size but too many pixels moved.</summary>
    Differs = 1,

    /// <summary>
    /// The images are different sizes, so no pixel comparison was attempted. Usually a window was
    /// resized or the display scaling changed between runs.
    /// </summary>
    SizeMismatch = 2,
}
