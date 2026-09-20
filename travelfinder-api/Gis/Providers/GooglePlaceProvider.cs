using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis.Providers;

public sealed class GooglePlaceProvider : IPlaceProvider
{
    private const string FieldMask = "places.displayName,places.formattedAddress,places.primaryType,places.rating,places.location,places.id,places.priceLevel";
    private const int MaxResults = 20;

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string? _apiKey;
    private int _lastCacheHits;

    public GooglePlaceProvider(HttpClient httpClient, IMemoryCache cache, string? apiKey = null)
    {
        _httpClient = httpClient;
        _cache = cache;
        _apiKey = apiKey;
    }

    public string Name => "google";

    public int LastCacheHits => _lastCacheHits;

    public async Task<IReadOnlyList<Contracts.Place>> SearchText(
        string query,
        double latitude,
        double longitude,
        int radiusMeters,
        string language,
        CancellationToken cancellationToken)
    {
        _lastCacheHits = 0;
        var point = new GeoPoint(latitude, longitude);
        var cacheKey = PlaceCacheKeys.Text(Name, query, point, radiusMeters, language);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Contracts.Place>? cached) && cached is not null)
        {
            _lastCacheHits++;
            return cached;
        }

        try
        {
            var requestBody = new
            {
                textQuery = query,
                languageCode = language,
                maxResultCount = MaxResults,
                locationBias = new
                {
                    circle = new
                    {
                        center = new { latitude, longitude },
                        radius = radiusMeters
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchText")
            {
                Content = JsonContent.Create(requestBody)
            };
            AddPlacesHeaders(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<Contracts.Place>();

            var payload = await response.Content.ReadFromJsonAsync<PlacesResponse>(cancellationToken: cancellationToken);
            var places = MapPlaces(payload?.Places).Take(MaxResults).ToArray();
            _cache.Set(cacheKey, places, PlaceCacheKeys.TextTtl);
            return places;
        }
        catch
        {
            return Array.Empty<Contracts.Place>();
        }
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
        var includedTypes = AllowedGoogleTypes.Clip(categories);
        if (includedTypes.Length == 0)
            includedTypes = AllowedGoogleTypes.All.ToArray();

        var cacheKey = PlaceCacheKeys.Nearby(Name, point, radiusMeters, includedTypes, language);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Contracts.Place>? cached) && cached is not null)
        {
            _lastCacheHits++;
            return cached;
        }

        try
        {
            var requestBody = new
            {
                includedTypes,
                languageCode = language,
                maxResultCount = MaxResults,
                locationRestriction = new
                {
                    circle = new
                    {
                        center = new { latitude, longitude },
                        radius = radiusMeters
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchNearby")
            {
                Content = JsonContent.Create(requestBody)
            };
            AddPlacesHeaders(request);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<Contracts.Place>();

            var payload = await response.Content.ReadFromJsonAsync<PlacesResponse>(cancellationToken: cancellationToken);
            var places = MapPlaces(payload?.Places).Take(MaxResults).ToArray();
            _cache.Set(cacheKey, places, PlaceCacheKeys.NearbyTtl);
            return places;
        }
        catch
        {
            return Array.Empty<Contracts.Place>();
        }
    }

    public async Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken)
    {
        _lastCacheHits = 0;
        var point = new GeoPoint(latitude, longitude);
        var cacheKey = PlaceCacheKeys.Reverse(point);
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _lastCacheHits++;
            return cached;
        }

        try
        {
            var apiKey = ResolveApiKey();
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?result_type=administrative_area_level_1&latlng={latitude},{longitude}&key={apiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return "";

            var payload = await response.Content.ReadFromJsonAsync<GeocodeResponse>(cancellationToken: cancellationToken);
            if (payload?.Results is null || payload.Results.Count == 0 || payload.Status is not ("OK" or "ZERO_RESULTS"))
            {
                if (payload?.Status == "ZERO_RESULTS" || payload?.Results?.Count == 0)
                    return "";

                return "";
            }

            var label = ExtractAreaLabel(payload.Results[0]);
            _cache.Set(cacheKey, label, PlaceCacheKeys.ReverseTtl);
            return label;
        }
        catch
        {
            return "";
        }
    }

    private void AddPlacesHeaders(HttpRequestMessage request)
    {
        var apiKey = ResolveApiKey();
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);

        request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", FieldMask);
    }

    private string? ResolveApiKey()
    {
        if (!string.IsNullOrEmpty(_apiKey))
            return _apiKey;

        if (_httpClient.DefaultRequestHeaders.TryGetValues("X-Goog-Api-Key", out var values))
            return values.FirstOrDefault();

        return null;
    }

    private static IReadOnlyList<Contracts.Place> MapPlaces(IEnumerable<GooglePlace>? places)
    {
        if (places is null)
            return Array.Empty<Contracts.Place>();

        return places
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .Select(p => new Contracts.Place
            {
                Id = Contracts.Place.ComposeId(PlaceSource.Google, p.Id!),
                Source = PlaceSource.Google,
                SourceId = p.Id!,
                Name = p.DisplayName?.Text ?? "",
                Address = p.FormattedAddress,
                PrimaryType = p.PrimaryType,
                Categories = string.IsNullOrEmpty(p.PrimaryType) ? [] : [p.PrimaryType],
                Rating = p.Rating,
                PriceLevel = p.PriceLevel,
                Location = new GeoPoint(p.Location?.Latitude ?? 0, p.Location?.Longitude ?? 0)
            })
            .ToArray();
    }

    private static string ExtractAreaLabel(GeocodeResult result)
    {
        var admin = result.AddressComponents?
            .FirstOrDefault(c => c.Types?.Contains("administrative_area_level_1") == true);

        if (!string.IsNullOrEmpty(admin?.LongName))
            return admin.LongName;

        return result.FormattedAddress ?? "";
    }

    private sealed class PlacesResponse
    {
        [JsonPropertyName("places")]
        public List<GooglePlace>? Places { get; set; }
    }

    private sealed class GooglePlace
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("displayName")]
        public GoogleDisplayName? DisplayName { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("primaryType")]
        public string? PrimaryType { get; set; }

        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("priceLevel")]
        public string? PriceLevel { get; set; }

        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; set; }
    }

    private sealed class GoogleDisplayName
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    private sealed class GeocodeResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodeResult>? Results { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed class GeocodeResult
    {
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("address_components")]
        public List<AddressComponent>? AddressComponents { get; set; }
    }

    private sealed class AddressComponent
    {
        [JsonPropertyName("long_name")]
        public string? LongName { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }
    }
}
