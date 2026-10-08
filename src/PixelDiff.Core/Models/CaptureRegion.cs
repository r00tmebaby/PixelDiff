using System.Text.Json.Serialization;

namespace PixelDiff.Core.Models;

/// <summary>
/// An axis aligned rectangle expressed in physical pixels. Screen captures use virtual
/// desktop coordinates, where the primary monitor starts at the origin and monitors
/// placed above or to the left of it produce negative offsets.
/// </summary>
/// <remarks>
/// The four constructor parameters are the whole state. Everything derived from them is kept out
/// of the serialised form, because a descriptor on disk is meant to be read and edited by hand and
/// a stored copy of a derived value can only ever contradict the value it was derived from.
/// </remarks>
/// <param name="X">Distance from the left edge of the coordinate space.</param>
/// <param name="Y">Distance from the top edge of the coordinate space.</param>
/// <param name="Width">Horizontal size in pixels.</param>
/// <param name="Height">Vertical size in pixels.</param>
public readonly record struct CaptureRegion(int X, int Y, int Width, int Height)
{
    /// <summary>Coordinate one pixel past the right edge.</summary>
    [JsonIgnore]
    public int Right => X + Width;

    /// <summary>Coordinate one pixel past the bottom edge.</summary>
    [JsonIgnore]
    public int Bottom => Y + Height;

    /// <summary>Pixel count covered by the rectangle, widened to avoid overflow on large captures.</summary>
    [JsonIgnore]
    public long Area => (long)Width * Height;

    /// <summary>True when the rectangle covers no pixels at all.</summary>
    [JsonIgnore]
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Builds a region from edge coordinates rather than from a size.</summary>
    public static CaptureRegion FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    /// <summary>
    /// Returns the part of this region that falls inside <paramref name="bounds"/>. The result is
    /// empty when the two rectangles do not overlap, which callers treat as nothing to do rather
    /// than as an error.
    /// </summary>
    public CaptureRegion IntersectWith(CaptureRegion bounds)
    {
        int left = Math.Max(X, bounds.X);
        int top = Math.Max(Y, bounds.Y);
        int right = Math.Min(Right, bounds.Right);
        int bottom = Math.Min(Bottom, bounds.Bottom);

        return right <= left || bottom <= top
            ? default
            : FromEdges(left, top, right, bottom);
    }

    /// <summary>
    /// Throws when the region cannot be captured or compared. A negative origin is legitimate on
    /// multi monitor setups, so only the size is rejected.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The width or height is not positive.</exception>
    public void EnsureNotEmpty()
    {
        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), Width, "Region width must be positive.");
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), Height, "Region height must be positive.");
        }
    }

    /// <summary>Renders the region the way most window tools print it, as position plus size.</summary>
    public override string ToString() => $"{Width}x{Height} at {X},{Y}";
}
