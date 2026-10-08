using System.Text.Json;
using PixelDiff.Core.Models;
using Xunit;

namespace PixelDiff.Core.Tests;

public sealed class CaptureRegionTests
{
    [Fact]
    public void IntersectWithReturnsTheOverlap()
    {
        var left = new CaptureRegion(0, 0, 100, 100);
        var right = new CaptureRegion(50, 60, 100, 100);

        Assert.Equal(new CaptureRegion(50, 60, 50, 40), left.IntersectWith(right));
    }

    [Fact]
    public void IntersectWithReturnsEmptyForDisjointRectangles()
    {
        var left = new CaptureRegion(0, 0, 10, 10);
        var right = new CaptureRegion(20, 20, 10, 10);

        Assert.True(left.IntersectWith(right).IsEmpty);
    }

    [Fact]
    public void IntersectWithReturnsEmptyForRectanglesThatOnlyTouch()
    {
        var left = new CaptureRegion(0, 0, 10, 10);
        var right = new CaptureRegion(10, 0, 10, 10);

        Assert.True(left.IntersectWith(right).IsEmpty);
    }

    [Fact]
    public void IntersectWithHandlesNegativeOrigins()
    {
        // A monitor placed left of and above the primary one yields negative coordinates.
        var secondary = new CaptureRegion(-1920, -200, 1920, 1080);
        var primary = new CaptureRegion(0, 0, 1920, 1080);
        var selection = new CaptureRegion(-100, -50, 200, 200);

        Assert.Equal(new CaptureRegion(-100, -50, 100, 200), secondary.IntersectWith(selection));
        Assert.True(secondary.IntersectWith(primary).IsEmpty);
    }

    [Fact]
    public void FromEdgesRoundTripsWithRightAndBottom()
    {
        CaptureRegion region = CaptureRegion.FromEdges(-5, 10, 25, 40);

        Assert.Equal(25, region.Right);
        Assert.Equal(40, region.Bottom);
        Assert.Equal(30, region.Width);
        Assert.Equal(30, region.Height);
        Assert.Equal(region, CaptureRegion.FromEdges(region.X, region.Y, region.Right, region.Bottom));
    }

    [Fact]
    public void AreaDoesNotOverflowForALargeRegion()
    {
        // 50000 squared exceeds Int32.MaxValue, so a 32 bit product would wrap negative.
        var region = new CaptureRegion(0, 0, 50_000, 50_000);

        Assert.Equal(2_500_000_000L, region.Area);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void EnsureNotEmptyThrowsForANonPositiveSize(int width, int height)
    {
        var region = new CaptureRegion(0, 0, width, height);

        Assert.Throws<ArgumentOutOfRangeException>(region.EnsureNotEmpty);
    }

    [Fact]
    public void EnsureNotEmptyAcceptsANegativeOrigin()
    {
        var region = new CaptureRegion(-1920, -1080, 1920, 1080);

        region.EnsureNotEmpty();
    }

    [Fact]
    public void OnlyTheFourStoredValuesAreWrittenToJson()
    {
        string json = JsonSerializer.Serialize(new CaptureRegion(3, 4, 5, 6));

        Assert.Equal("{\"X\":3,\"Y\":4,\"Width\":5,\"Height\":6}", json);
    }

    [Fact]
    public void DerivedValuesLeftInAnOlderFileAreIgnoredWhenReading()
    {
        // Descriptors written before the derived members were excluded carry all eight values.
        // They must keep loading, and the contradictory ones must not win over the real state.
        const string Json =
            "{\"X\":3,\"Y\":4,\"Width\":5,\"Height\":6,\"Right\":999,\"Bottom\":999,\"Area\":1,\"IsEmpty\":true}";

        CaptureRegion region = JsonSerializer.Deserialize<CaptureRegion>(Json);

        Assert.Equal(new CaptureRegion(3, 4, 5, 6), region);
        Assert.Equal(8, region.Right);
        Assert.Equal(10, region.Bottom);
        Assert.Equal(30, region.Area);
        Assert.False(region.IsEmpty);
    }
}
