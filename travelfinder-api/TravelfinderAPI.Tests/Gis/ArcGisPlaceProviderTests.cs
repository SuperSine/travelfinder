using System.Net;
using TravelfinderAPI.Gis.Providers;
using Xunit;

namespace TravelfinderAPITests.Gis;

public class ArcGisPlaceProviderTests
{
    [Fact]
    public async Task Nearby_maps_feature_layer_rows()
    {
        var json = """
            {"features":[{"attributes":{"OBJECTID":7,"Name":"Secret Lookout","Category":"park","FormattedAddress":"Hill"},"geometry":{"x":103.846,"y":1.295}}]}
            """;
        var http = new HttpClient(new FakeHttpMessageHandler(json)) { BaseAddress = new Uri("https://services8.arcgis.com/") };
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://services8.arcgis.com/layer/FeatureServer/0");

        var places = await provider.Nearby(1.3, 103.8, 5000, "en-us", [], CancellationToken.None);

        Assert.Single(places);
        Assert.Equal("arcgis:7", places[0].Id);
        Assert.Equal("Secret Lookout", places[0].Name);
        Assert.Equal(1.295, places[0].Location.Latitude);
    }

    [Fact]
    public async Task SearchText_is_empty_this_slice()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("{}"));
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://example/layer");

        Assert.Empty(await provider.SearchText("x", 1, 2, 5000, "en-us", CancellationToken.None));
        Assert.Equal("", await provider.ReverseGeocode(1, 2, CancellationToken.None));
    }

    [Fact]
    public async Task Nearby_returns_empty_when_layer_fails()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("down", HttpStatusCode.BadGateway));
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://example/layer");

        Assert.Empty(await provider.Nearby(1, 2, 5000, "en-us", [], CancellationToken.None));
    }

    private static Microsoft.Extensions.Caching.Memory.IMemoryCache MemoryCache() =>
        new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
}
