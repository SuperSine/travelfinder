using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPITests.Gis;

public class PlaceCacheKeyTests
{
    [Fact]
    public void Same_geohash7_and_radius_bucket_hits()
    {
        var a = PlaceCacheKeys.Nearby("google", new GeoPoint(1.35210, 103.81980), 5000, ["park"], "en-us");
        var b = PlaceCacheKeys.Nearby("google", new GeoPoint(1.35212, 103.81982), 5000, ["park"], "en-us");

        Assert.Equal(a, b);
        Assert.StartsWith("nearby|google|", a);
    }

    [Fact]
    public void Category_change_misses()
    {
        var a = PlaceCacheKeys.Nearby("google", new GeoPoint(1.3521, 103.8198), 5000, ["park"], "en-us");
        var b = PlaceCacheKeys.Nearby("google", new GeoPoint(1.3521, 103.8198), 5000, ["cafe"], "en-us");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Text_key_normalizes_query()
    {
        var a = PlaceCacheKeys.Text("google", "  Fort   Canning ", new GeoPoint(1.3521, 103.8198), 5000, "en-us");
        var b = PlaceCacheKeys.Text("google", "fort canning", new GeoPoint(1.3521, 103.8198), 5000, "en-us");

        Assert.Equal(a, b);
        Assert.StartsWith("text|google|fort canning|", a);
    }

    [Fact]
    public void Reverse_uses_geohash8_and_24h_ttl()
    {
        var key = PlaceCacheKeys.Reverse(new GeoPoint(1.3521, 103.8198));

        Assert.StartsWith("rev|", key);
        Assert.Equal(TimeSpan.FromHours(24), PlaceCacheKeys.ReverseTtl);
        Assert.Equal(TimeSpan.FromMinutes(10), PlaceCacheKeys.NearbyTtl);
    }
}
