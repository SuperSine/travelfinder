using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public interface IPlaceService
{
    Task<IReadOnlyList<Contracts.Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<Contracts.Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken);
    Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken);
    Task<MergedPlacesResult> GetMergedPlaces(PlanSpec spec, GeoPoint origin, string language, CancellationToken cancellationToken);
}

public sealed class MergedPlacesResult
{
    public required IReadOnlyList<Contracts.Place> Places { get; init; }
    public int GoogleCount { get; init; }
    public int ArcgisCount { get; init; }
    public int CacheHits { get; init; }
    public bool PartialFailure { get; init; }
}
