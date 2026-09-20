using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPITests.Gis;

public class PlaceServiceTests
{
    [Fact]
    public async Task Google_throw_still_returns_arcgis_rows()
    {
        var google = new StubProvider("google") { ThrowOnNearby = true, ThrowOnText = true };
        var arcgis = new StubProvider("arcgis");
        arcgis.NearbyResult.Add(Place("arcgis", "1", "User Spot", 1.3, 103.8));
        var service = new PlaceService([google, arcgis]);
        var spec = new PlanSpec { Categories = ["park"], RadiusMeters = 5000 };

        var result = await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Single(result.Places);
        Assert.Equal("arcgis:1", result.Places[0].Id);
        Assert.True(result.PartialFailure);
        Assert.Equal(0, result.GoogleCount);
        Assert.Equal(1, result.ArcgisCount);
    }

    [Fact]
    public async Task Empty_merge_is_zero_places()
    {
        var service = new PlaceService([new StubProvider("google"), new StubProvider("arcgis")]);
        var result = await service.GetMergedPlaces(new PlanSpec(), new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Empty(result.Places);
    }

    [Fact]
    public async Task Radius_is_clamped_before_provider_calls()
    {
        var google = new StubProvider("google");
        var service = new PlaceService([google, new StubProvider("arcgis")]);
        var spec = new PlanSpec { RadiusMeters = 5, PointOfInterests = ["coffee"] };

        await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Equal(500, spec.RadiusMeters);
        Assert.Equal(500, google.LastRadius);
    }

    [Fact]
    public async Task Fanout_runs_each_poi_text_plus_nearby_plus_arcgis()
    {
        var google = new StubProvider("google");
        var arcgis = new StubProvider("arcgis");
        var service = new PlaceService([google, arcgis]);
        var spec = new PlanSpec
        {
            PointOfInterests = ["fort canning", "hawker"],
            Categories = ["park"]
        };

        await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Equal(2, google.TextQueries.Count);
        Assert.True(google.NearbyCalled);
        Assert.True(arcgis.NearbyCalled);
    }

    private static Place Place(string source, string id, string name, double lat, double lng) =>
        new()
        {
            Id = TravelfinderAPI.Contracts.Place.ComposeId(source == "google" ? PlaceSource.Google : PlaceSource.Arcgis, id),
            Source = source == "google" ? PlaceSource.Google : PlaceSource.Arcgis,
            SourceId = id,
            Name = name,
            Categories = ["park"],
            Location = new GeoPoint(lat, lng)
        };

    private sealed class StubProvider : IPlaceProvider
    {
        public StubProvider(string name) => Name = name;
        public string Name { get; }
        public int LastCacheHits { get; set; }
        public bool ThrowOnNearby { get; set; }
        public bool ThrowOnText { get; set; }
        public bool NearbyCalled { get; private set; }
        public int LastRadius { get; private set; }
        public List<string> TextQueries { get; } = [];
        public List<Place> NearbyResult { get; } = [];
        public List<Place> TextResult { get; } = [];

        public Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken)
        {
            LastRadius = radiusMeters;
            TextQueries.Add(query);
            if (ThrowOnText) throw new HttpRequestException("google down");
            return Task.FromResult<IReadOnlyList<Place>>(TextResult);
        }

        public Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken)
        {
            NearbyCalled = true;
            LastRadius = radiusMeters;
            if (ThrowOnNearby) throw new HttpRequestException("google down");
            return Task.FromResult<IReadOnlyList<Place>>(NearbyResult);
        }

        public Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken) =>
            Task.FromResult("Singapore");
    }
}
