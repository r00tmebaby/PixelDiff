using PixelDiff.Core.Comparison;

namespace PixelDiff.Cli;

/// <summary>
/// What a run recorded for one baseline. The first three values carry the same names and meanings
/// as <see cref="ComparisonOutcome"/>, so a reader of <c>report.json</c> sees no change for them.
/// <see cref="Error"/> exists because a run can fail before a comparison happens at all, which the
/// comparer has no vocabulary for.
/// </summary>
public enum RunOutcome
{
    /// <summary>The capture agrees with the approved image within the configured tolerances.</summary>
    Match = 0,

    /// <summary>The images are the same size but too many pixels moved.</summary>
    Differs = 1,

    /// <summary>The images are different sizes, so no pixel comparison was attempted.</summary>
    SizeMismatch = 2,

    /// <summary>
    /// The baseline could not be compared at all, because it could not be read or its target could
    /// not be captured. The reason is in the entry detail.
    /// </summary>
    Error = 3,
}
