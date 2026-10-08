namespace PixelDiff.Core.Models;

/// <summary>What a baseline is a picture of.</summary>
public enum CaptureTargetKind
{
    /// <summary>A fixed rectangle of the virtual desktop.</summary>
    ScreenRegion = 0,

    /// <summary>A top level window located by title, so it survives being moved between runs.</summary>
    Window = 1,
}
