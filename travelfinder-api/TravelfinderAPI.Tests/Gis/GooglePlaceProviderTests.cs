using System.Net;
using System.Text;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Gis.Providers;
using Xunit;

namespace TravelfinderAPITests.Gis;

public class GooglePlaceProviderTests
{
    [Fact]
    public async Task SearchText_maps_display_name_and_composes_id()
    {
        var json = """
            {"places":[{"id":"abc","displayName":{"text":"Fort Canning"},"formattedAddress":"River Valley Rd","primaryType":"park","rating":4.6,"location":{"latitude":1.295,"longitude":103.846},"priceLevel":"PRICE_LEVEL_MODERATE"}]}
            """;
        var http = new HttpClient(new FakeHttpMessageHandler(json)) { BaseAddress = new Uri("https://places.googleapis.com/") };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var places = await provider.SearchText("fort canning", 1.3, 103.8, 5000, "en-us", CancellationToken.None);

        Assert.Single(places);
        Assert.Equal("google:abc", places[0].Id);
        Assert.Equal("Fort Canning", places[0].Name);
        Assert.Equal(4.6, places[0].Rating);
    }

    [Fact]
    public async Task Nearby_returns_empty_on_http_failure()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("nope", HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("https://places.googleapis.com/")
        };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var places = await provider.Nearby(1.3, 103.8, 5000, "en-us", ["park"], CancellationToken.None);

        Assert.Empty(places);
    }

    [Fact]
    public async Task ReverseGeocode_falls_back_to_empty_string()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("""{"results":[],"status":"ZERO_RESULTS"}"""))
        {
            BaseAddress = new Uri("https://maps.googleapis.com/")
        };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var label = await provider.ReverseGeocode(1.3, 103.8, CancellationToken.None);

        Assert.Equal("", label);
    }

    [Fact]
    public async Task ReverseGeocode_uses_maps_host_when_base_address_is_places()
    {
        var handler = new FakeHttpMessageHandler("""{"results":[],"status":"ZERO_RESULTS"}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://places.googleapis.com/") };
        var provider = new GooglePlaceProvider(http, MemoryCache(), "test-key");

        await provider.ReverseGeocode(1.3, 103.8, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("maps.googleapis.com", handler.LastRequest!.RequestUri!.Host);
        Assert.StartsWith("https://maps.googleapis.com/maps/api/geocode/json", handler.LastRequest.RequestUri.ToString());
    }

    [Fact]
    public async Task LastCacheHits_is_zero_after_cache_miss_following_hit()
    {
        var json = """
            {"places":[{"id":"abc","displayName":{"text":"Fort Canning"},"formattedAddress":"River Valley Rd","primaryType":"park","rating":4.6,"location":{"latitude":1.295,"longitude":103.846},"priceLevel":"PRICE_LEVEL_MODERATE"}]}
            """;
        var http = new HttpClient(new FakeHttpMessageHandler(json)) { BaseAddress = new Uri("https://places.googleapis.com/") };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        await provider.SearchText("fort canning", 1.3, 103.8, 5000, "en-us", CancellationToken.None);
        await provider.SearchText("fort canning", 1.3, 103.8, 5000, "en-us", CancellationToken.None);
        Assert.Equal(1, provider.LastCacheHits);

        await provider.SearchText("other query", 1.3, 103.8, 5000, "en-us", CancellationToken.None);
        Assert.Equal(0, provider.LastCacheHits);
    }

    private static Microsoft.Extensions.Caching.Memory.IMemoryCache MemoryCache() =>
        new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
}
