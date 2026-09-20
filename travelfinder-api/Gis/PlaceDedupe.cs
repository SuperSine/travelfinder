using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceDedupe
{
    public const double CollapseMeters = 80;

    public static IReadOnlyList<Contracts.Place> Merge(IEnumerable<Contracts.Place> places)
    {
        var byId = new Dictionary<string, Contracts.Place>(StringComparer.Ordinal);
        foreach (var place in places)
        {
            if (byId.TryGetValue(place.Id, out var existing))
            {
                byId[place.Id] = Prefer(existing, place);
            }
            else
            {
                byId[place.Id] = place;
            }
        }

        var remaining = byId.Values.ToList();
        var kept = new List<Contracts.Place>();

        foreach (var candidate in remaining)
        {
            var matchIndex = kept.FindIndex(existing => IsNearDuplicate(existing, candidate));
            if (matchIndex < 0)
            {
                kept.Add(candidate);
                continue;
            }

            kept[matchIndex] = Prefer(kept[matchIndex], candidate);
        }

        return kept;
    }

    private static bool IsNearDuplicate(Contracts.Place a, Contracts.Place b)
    {
        if (GeoMath.NormalizeName(a.Name) != GeoMath.NormalizeName(b.Name))
        {
            return false;
        }

        return GeoMath.DistanceMeters(a.Location, b.Location) <= CollapseMeters;
    }

    private static Contracts.Place Prefer(Contracts.Place a, Contracts.Place b)
    {
        var aRated = a.Rating.HasValue;
        var bRated = b.Rating.HasValue;
        if (aRated != bRated)
        {
            return aRated ? a : b;
        }

        if (a.Source != b.Source)
        {
            return a.Source == PlaceSource.Google ? a : b;
        }

        return a;
    }
}
