using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public sealed class PlaceService : IPlaceService
{
    private const int PerSourceCap = 20;
    private const int MergedCap = 40;

    private readonly IReadOnlyList<IPlaceProvider> _providers;

    public PlaceService(IEnumerable<IPlaceProvider> providers) =>
        _providers = providers.ToList();

    public async Task<IReadOnlyList<Contracts.Place>> SearchText(
        string query,
        double latitude,
        double longitude,
        int radiusMeters,
        string language,
        CancellationToken cancellationToken)
    {
        var provider = PrimaryProvider();
        if (provider == null)
        {
            return [];
        }

        return await provider.SearchText(query, latitude, longitude, radiusMeters, language, cancellationToken);
    }

    public async Task<IReadOnlyList<Contracts.Place>> Nearby(
        double latitude,
        double longitude,
        int radiusMeters,
        string language,
        string[] categories,
        CancellationToken cancellationToken)
    {
        var provider = PrimaryProvider();
        if (provider == null)
        {
            return [];
        }

        return await provider.Nearby(latitude, longitude, radiusMeters, language, categories, cancellationToken);
    }

    public async Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var provider = PrimaryProvider();
        if (provider == null)
        {
            return "";
        }

        return await provider.ReverseGeocode(latitude, longitude, cancellationToken);
    }

    public async Task<MergedPlacesResult> GetMergedPlaces(
        PlanSpec spec,
        GeoPoint origin,
        string language,
        CancellationToken cancellationToken)
    {
        spec.ClampRadius();
        var radius = spec.RadiusMeters;
        var partialFailure = false;
        var googleRows = new List<Contracts.Place>();
        var arcgisRows = new List<Contracts.Place>();
        var tasks = new List<Task>();

        var google = FindProvider("google");
        var arcgis = FindProvider("arcgis");

        if (google != null)
        {
            foreach (var term in spec.PointOfInterests)
            {
                var query = term;
                tasks.Add(CollectAsync(
                    () => google.SearchText(query, origin.Latitude, origin.Longitude, radius, language, cancellationToken),
                    googleRows,
                    () => partialFailure = true));
            }

            if (spec.Categories.Length > 0)
            {
                var clipped = AllowedGoogleTypes.Clip(spec.Categories);
                tasks.Add(CollectAsync(
                    () => google.Nearby(origin.Latitude, origin.Longitude, radius, language, clipped, cancellationToken),
                    googleRows,
                    () => partialFailure = true));
            }
        }

        if (arcgis != null)
        {
            tasks.Add(CollectAsync(
                () => arcgis.Nearby(origin.Latitude, origin.Longitude, radius, language, spec.Categories, cancellationToken),
                arcgisRows,
                () => partialFailure = true));
        }

        await Task.WhenAll(tasks);

        var cappedGoogle = googleRows.Take(PerSourceCap).ToList();
        var cappedArcgis = arcgisRows.Take(PerSourceCap).ToList();
        var merged = PlaceDedupe.Merge(cappedGoogle.Concat(cappedArcgis));
        var scored = PlaceScoring.Apply(merged.ToList(), spec, origin);
        var places = scored.Take(MergedCap).ToList();

        return new MergedPlacesResult
        {
            Places = places,
            GoogleCount = cappedGoogle.Count,
            ArcgisCount = cappedArcgis.Count,
            CacheHits = _providers.Sum(p => p.LastCacheHits),
            PartialFailure = partialFailure
        };
    }

    private IPlaceProvider? PrimaryProvider() =>
        FindProvider("google") ?? FindProvider("arcgis");

    private IPlaceProvider? FindProvider(string name) =>
        _providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static async Task CollectAsync(
        Func<Task<IReadOnlyList<Contracts.Place>>> call,
        List<Contracts.Place> target,
        Action onFailure)
    {
        try
        {
            var rows = await call();
            target.AddRange(rows);
        }
        catch
        {
            onFailure();
        }
    }
}
