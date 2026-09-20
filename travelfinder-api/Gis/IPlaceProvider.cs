using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public interface IPlaceProvider
{
    string Name { get; }
    int LastCacheHits { get; }
    Task<IReadOnlyList<Contracts.Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<Contracts.Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken);
    Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken);
}
