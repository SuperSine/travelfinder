using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceScoring
{
    public static IReadOnlyList<Contracts.Place> Apply(IReadOnlyList<Contracts.Place> places, PlanSpec spec, GeoPoint origin)
    {
        foreach (var place in places)
        {
            place.Score = 0.45 * TypeMatch(place, spec)
                          + 0.25 * DistanceScore(place, spec, origin)
                          + 0.20 * RatingScore(place)
                          + 0.10 * SourcePrior(place);
        }

        return places.OrderByDescending(p => p.Score).ToList();
    }

    private static double TypeMatch(Contracts.Place place, PlanSpec spec)
    {
        var needles = spec.Categories.Concat(spec.PointOfInterests)
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet();
        if (needles.Count == 0)
        {
            return 0.5;
        }

        var hay = place.Categories.Append(place.PrimaryType ?? "").Append(place.Name)
            .Select(s => s.ToLowerInvariant());
        return hay.Any(h => needles.Any(n => h.Contains(n, StringComparison.Ordinal))) ? 1.0 : 0.0;
    }

    private static double DistanceScore(Contracts.Place place, PlanSpec spec, GeoPoint origin)
    {
        var radius = spec.RadiusMeters <= 0 ? PlanSpec.DefaultRadiusMeters : spec.RadiusMeters;
        var meters = GeoMath.DistanceMeters(origin, place.Location);
        return Math.Clamp(1.0 - (meters / radius), 0, 1);
    }

    private static double RatingScore(Contracts.Place place) => place.Rating.HasValue ? Math.Clamp(place.Rating.Value / 5.0, 0, 1) : 0.5;

    private static double SourcePrior(Contracts.Place place) => place.Source == PlaceSource.Arcgis ? 0.8 : 0.6;
}
