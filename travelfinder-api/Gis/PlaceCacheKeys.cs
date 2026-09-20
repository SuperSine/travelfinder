using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceCacheKeys
{
    public static readonly TimeSpan NearbyTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TextTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ReverseTtl = TimeSpan.FromHours(24);

    public static string Nearby(string provider, GeoPoint point, int radiusMeters, IEnumerable<string> types, string language)
    {
        var hash = TypesHash(types);
        return $"nearby|{provider}|{Geohash.Encode(point.Latitude, point.Longitude, 7)}|{RadiusBucket(radiusMeters)}|{hash}|{language.ToLowerInvariant()}";
    }

    public static string Text(string provider, string query, GeoPoint point, int radiusMeters, string language)
    {
        var normalized = Regex.Replace(query.Trim().ToLowerInvariant(), @"\s+", " ");
        return $"text|{provider}|{normalized}|{Geohash.Encode(point.Latitude, point.Longitude, 7)}|{RadiusBucket(radiusMeters)}|{language.ToLowerInvariant()}";
    }

    public static string Reverse(GeoPoint point) =>
        $"rev|{Geohash.Encode(point.Latitude, point.Longitude, 8)}";

    public static int RadiusBucket(int radiusMeters) =>
        Math.Clamp(radiusMeters, PlanSpec.MinRadiusMeters, PlanSpec.MaxRadiusMeters) / 500;

    private static string TypesHash(IEnumerable<string> types)
    {
        var joined = string.Join(",", types.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).OrderBy(t => t));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
