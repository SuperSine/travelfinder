using System.Text.RegularExpressions;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class GeoMath
{
    private static readonly Regex CollapseWs = new(@"\s+", RegexOptions.Compiled);
    private static readonly string[] TrivialSuffixes = ["park", "restaurant", "cafe", "bar", "store", "museum", "gallery"];

    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        const double earth = 6371000;
        var dLat = DegreesToRadians(b.Latitude - a.Latitude);
        var dLon = DegreesToRadians(b.Longitude - a.Longitude);
        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earth * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static string NormalizeName(string name)
    {
        var n = CollapseWs.Replace(name.Trim().ToLowerInvariant(), " ");
        foreach (var suffix in TrivialSuffixes)
        {
            if (n.EndsWith(" " + suffix, StringComparison.Ordinal))
            {
                n = n[..^ (suffix.Length + 1)].Trim();
            }
        }

        return n;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
