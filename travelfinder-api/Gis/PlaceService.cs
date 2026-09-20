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
        var googleTasks = new List<Task<(IReadOnlyList<Contracts.Place> Rows, bool Failed)>>();
        var arcgisTasks = new List<Task<(IReadOnlyList<Contracts.Place> Rows, bool Failed)>>();

        var google = FindProvider("google");
        var arcgis = FindProvider("arcgis");

        if (google != null)
        {
            foreach (var term in spec.PointOfInterests)
            {
                var query = term;
                googleTasks.Add(CollectAsync(
                    () => google.SearchText(query, origin.Latitude, origin.Longitude, radius, language, cancellationToken),
                    cancellationToken));
            }

            if (spec.Categories.Length > 0)
            {
                var clipped = AllowedGoogleTypes.Clip(spec.Categories);
                googleTasks.Add(CollectAsync(
                    () => google.Nearby(origin.Latitude, origin.Longitude, radius, language, clipped, cancellationToken),
                    cancellationToken));
            }
        }

        if (arcgis != null)
        {
            arcgisTasks.Add(CollectAsync(
                () => arcgis.Nearby(origin.Latitude, origin.Longitude, radius, language, spec.Categories, cancellationToken),
                cancellationToken));
        }

        await Task.WhenAll(googleTasks.Concat<Task>(arcgisTasks));

        var googleResults = googleTasks.Select(t => t.Result).ToList();
        var arcgisResults = arcgisTasks.Select(t => t.Result).ToList();
        var partialFailure = googleResults.Any(r => r.Failed) || arcgisResults.Any(r => r.Failed);
        var allGoogle = googleResults.SelectMany(r => r.Rows).ToList();
        var allArcgis = arcgisResults.SelectMany(r => r.Rows).ToList();
        var merged = PlaceDedupe.Merge(allGoogle.Concat(allArcgis));
        var scored = PlaceScoring.Apply(merged.ToList(), spec, origin);
        var cappedGoogle = scored.Where(p => p.Source == PlaceSource.Google).Take(PerSourceCap).ToList();
        var cappedArcgis = scored.Where(p => p.Source == PlaceSource.Arcgis).Take(PerSourceCap).ToList();
        var places = cappedGoogle
            .Concat(cappedArcgis)
            .OrderByDescending(p => p.Score)
            .Take(MergedCap)
            .ToList();

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

    private static async Task<(IReadOnlyList<Contracts.Place> Rows, bool Failed)> CollectAsync(
        Func<Task<IReadOnlyList<Contracts.Place>>> call,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await call();
            return (rows, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ([], true);
        }
    }
}
