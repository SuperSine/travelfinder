namespace TravelfinderAPI.Gis;

public static class AllowedGoogleTypes
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "park", "restaurant", "art_gallery", "museum", "historical_landmark",
        "cafe", "bar", "library", "night_club", "store", "jewelry_store"
    };

    public static string[] Clip(IEnumerable<string> categories) =>
        categories.Select(c => c.Trim()).Where(c => All.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
