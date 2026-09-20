using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis.Providers;

public sealed class ArcGisPlaceProvider : IPlaceProvider
{
    private const int MaxResults = 20;

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _pointLayerUrl;
    private readonly string? _token;
    private int _lastCacheHits;

    public ArcGisPlaceProvider(HttpClient httpClient, IMemoryCache cache, string pointLayerUrl, string? token = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _pointLayerUrl = pointLayerUrl.TrimEnd('/');
        _token = token;
    }

    public string Name => "arcgis";

    public int LastCacheHits => _lastCacheHits;

    public Task<IReadOnlyList<Contracts.Place>> SearchText(
        string query,
        double latitude,
        double longitude,
        int radiusMeters,
        string language,
        CancellationToken cancellationToken)
    {
        _lastCacheHits = 0;
        return Task.FromResult<IReadOnlyList<Contracts.Place>>(Array.Empty<Contracts.Place>());
    }

    public async Task<IReadOnlyList<Contracts.Place>> Nearby(
        double latitude,
        double longitude,
        int radiusMeters,
        string language,
        string[] categories,
        CancellationToken cancellationToken)
    {
        _lastCacheHits = 0;
        var point = new GeoPoint(latitude, longitude);
        var cacheKey = PlaceCacheKeys.Nearby(Name, point, radiusMeters, categories, language);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Contracts.Place>? cached) && cached is not null)
        {
            _lastCacheHits++;
            return cached;
        }

        try
        {
            var parameters = new Dictionary<string, string?>
            {
                ["f"] = "json",
                ["returnGeometry"] = "true",
                ["outFields"] = "*",
                ["geometry"] = $"{longitude},{latitude}",
                ["geometryType"] = "esriGeometryPoint",
                ["spatialRel"] = "esriSpatialRelIntersects",
                ["distance"] = radiusMeters.ToString(),
                ["units"] = "esriSRUnit_Meter",
                ["inSR"] = "4326",
                ["outSR"] = "4326",
                ["resultRecordCount"] = MaxResults.ToString()
            };

            var token = ResolveToken();
            if (!string.IsNullOrEmpty(token))
                parameters["token"] = token;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_pointLayerUrl}/query")
            {
                Content = new FormUrlEncodedContent(parameters)
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<Contracts.Place>();

            var payload = await response.Content.ReadFromJsonAsync<FeatureLayerResponse>(cancellationToken: cancellationToken);
            var places = MapFeatures(payload?.Features).Take(MaxResults).ToArray();
            _cache.Set(cacheKey, places, PlaceCacheKeys.NearbyTtl);
            return places;
        }
        catch
        {
            return Array.Empty<Contracts.Place>();
        }
    }

    public Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken)
    {
        _lastCacheHits = 0;
        return Task.FromResult("");
    }

    private string? ResolveToken()
    {
        if (!string.IsNullOrEmpty(_token))
            return _token;

        if (_httpClient.DefaultRequestHeaders.TryGetValues("X-ArcGIS-Token", out var values))
            return values.FirstOrDefault();

        return null;
    }

    private static IReadOnlyList<Contracts.Place> MapFeatures(IEnumerable<FeatureRecord>? features)
    {
        if (features is null)
            return Array.Empty<Contracts.Place>();

        return features
            .Where(f => f.Attributes?.ObjectId is not null)
            .Select(f =>
            {
                var objectId = f.Attributes!.ObjectId!.Value.ToString();
                var category = f.Attributes.Category;
                return new Contracts.Place
                {
                    Id = Contracts.Place.ComposeId(PlaceSource.Arcgis, objectId),
                    Source = PlaceSource.Arcgis,
                    SourceId = objectId,
                    Name = f.Attributes.Name ?? "",
                    Address = f.Attributes.FormattedAddress,
                    PrimaryType = category,
                    Categories = string.IsNullOrEmpty(category) ? [] : [category],
                    Location = new GeoPoint(f.Geometry?.Y ?? 0, f.Geometry?.X ?? 0)
                };
            })
            .ToArray();
    }

    private sealed class FeatureLayerResponse
    {
        [JsonPropertyName("features")]
        public List<FeatureRecord>? Features { get; set; }
    }

    private sealed class FeatureRecord
    {
        [JsonPropertyName("attributes")]
        public FeatureAttributes? Attributes { get; set; }

        [JsonPropertyName("geometry")]
        public FeatureGeometry? Geometry { get; set; }
    }

    private sealed class FeatureAttributes
    {
        [JsonPropertyName("OBJECTID")]
        public int? ObjectId { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Category")]
        public string? Category { get; set; }

        [JsonPropertyName("FormattedAddress")]
        public string? FormattedAddress { get; set; }
    }

    private sealed class FeatureGeometry
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
