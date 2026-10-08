namespace PixelDiff.App.ViewModels;

/// <summary>Which of the three images of a baseline the viewer currently shows.</summary>
public enum ImageViewMode
{
    /// <summary>The approved image.</summary>
    Baseline,

    /// <summary>The most recent capture.</summary>
    Candidate,

    /// <summary>The rendering that highlights what changed between the two.</summary>
    Diff,
}
