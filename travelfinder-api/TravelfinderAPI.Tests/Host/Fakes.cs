using System.Text.Json;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Host;

namespace TravelfinderAPITests.Host;

internal sealed class RecordingSseWriter : ISseWriter
{
    public List<string> Names { get; } = [];
    public List<string> Data { get; } = [];
    public async Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Names.Add(eventName);
        Data.Add(JsonSerializer.Serialize(payload, payload.GetType(), PlanJson.Options));
        await Task.CompletedTask;
    }
}

internal sealed class FakePlanner : IPlanner
{
    private readonly PlannerOutcome _outcome;
    private readonly Action? _onPlan;
    public FakePlanner(PlannerOutcome outcome, Action? onPlan = null) { _outcome = outcome; _onPlan = onPlan; }
    public Task<PlannerOutcome> PlanAsync(IReadOnlyList<ChatMessageDto> messages, double latitude, double longitude, string language, CancellationToken cancellationToken)
    {
        _onPlan?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_outcome);
    }
}

internal sealed class SlowPlanner : IPlanner
{
    private readonly TimeSpan _delay;

    public SlowPlanner(TimeSpan delay) => _delay = delay;

    public async Task<PlannerOutcome> PlanAsync(
        IReadOnlyList<ChatMessageDto> messages,
        double latitude,
        double longitude,
        string language,
        CancellationToken cancellationToken)
    {
        await Task.Delay(_delay, cancellationToken);
        return new PlannerOutcome { Spec = new PlanSpec() };
    }
}

internal sealed class FakeRenderer : IRenderer
{
    private readonly IReadOnlyList<ItineraryStop> _stops;
    public FakeRenderer(IReadOnlyList<ItineraryStop> stops) => _stops = stops;
    public async IAsyncEnumerable<ItineraryStop> RenderAsync(PlanSpec spec, IReadOnlyList<Place> places, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var stop in _stops)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return stop;
            await Task.Yield();
        }
    }
}

internal sealed class TrackingPlaceService : IPlaceService
{
    private readonly IReadOnlyList<Place> _places;
    public bool MergedCalled { get; private set; }
    public TrackingPlaceService(IReadOnlyList<Place>? places = null) => _places = places ?? [];
    public Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Place>>([]);
    public Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Place>>([]);
    public Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken) =>
        Task.FromResult("current location");
    public Task<MergedPlacesResult> GetMergedPlaces(PlanSpec spec, GeoPoint origin, string language, CancellationToken cancellationToken)
    {
        MergedCalled = true;
        return Task.FromResult(new MergedPlacesResult { Places = _places, GoogleCount = _places.Count, ArcgisCount = 0, CacheHits = 0 });
    }
}
