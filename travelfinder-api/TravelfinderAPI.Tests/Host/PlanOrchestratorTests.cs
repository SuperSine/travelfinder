using System.Text.Json;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPITests.Host;

public class PlanOrchestratorTests
{
    [Fact]
    public async Task Happy_path_emits_spec_places_delta_done()
    {
        var writer = new RecordingSseWriter();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Spec = new PlanSpec { AreaLabel = "Singapore", Categories = ["park"] } }),
            places: [SamplePlace()],
            stops: [new ItineraryStop { DayIndex = 0, StopIndex = 0, PlaceId = "google:1", Name = "Park" }]);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal(
            [PlanEventNames.PlanSpec, PlanEventNames.Places, PlanEventNames.PlanDelta, PlanEventNames.Done],
            writer.Names);
        Assert.DoesNotContain(PlanEventNames.Clarification, writer.Names);
    }

    [Fact]
    public async Task Clarification_emits_no_places()
    {
        var writer = new RecordingSseWriter();
        var places = new TrackingPlaceService();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Clarification = "How many days?" }),
            placeService: places);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal([PlanEventNames.Clarification, PlanEventNames.Done], writer.Names);
        Assert.False(places.MergedCalled);
    }

    [Fact]
    public async Task Client_abort_stops_further_events()
    {
        var writer = new RecordingSseWriter();
        using var cts = new CancellationTokenSource();
        var planner = new FakePlanner(new PlannerOutcome { Spec = new PlanSpec() }, onPlan: cts.Cancel);
        var orchestrator = Create(planner: planner, places: [SamplePlace()]);

        await orchestrator.RunAsync(ValidRequest(), writer, cts.Token);

        Assert.DoesNotContain(PlanEventNames.Places, writer.Names);
        Assert.DoesNotContain(PlanEventNames.PlanDelta, writer.Names);
    }

    [Fact]
    public async Task No_places_emits_error_then_done()
    {
        var writer = new RecordingSseWriter();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Spec = new PlanSpec { Categories = ["park"] } }),
            places: []);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal([PlanEventNames.PlanSpec, PlanEventNames.Error, PlanEventNames.Done], writer.Names);
        var error = JsonSerializer.Deserialize<ErrorPayload>(writer.Data[1], PlanJson.Options);
        Assert.Equal(PlanErrorCode.NoPlaces, error!.Code);
    }

    private static PlanOrchestrator Create(
        IPlanner planner,
        IReadOnlyList<Place>? places = null,
        IReadOnlyList<ItineraryStop>? stops = null,
        IPlaceService? placeService = null) =>
        new(planner, new FakeRenderer(stops ?? []), placeService ?? new TrackingPlaceService(places ?? []), NullLogger());

    private static PlanRequest ValidRequest() => new()
    {
        RequestId = "r1",
        Messages = [new ChatMessageDto { Role = "user", Content = "a day in the park" }],
        Latitude = 1.3521,
        Longitude = 103.8198,
        Language = "en-us"
    };

    private static Place SamplePlace() => new()
    {
        Id = "google:1",
        Source = PlaceSource.Google,
        SourceId = "1",
        Name = "Park",
        Categories = ["park"],
        Location = new GeoPoint(1.3, 103.8)
    };

    private static Microsoft.Extensions.Logging.ILogger<PlanOrchestrator> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanOrchestrator>.Instance;
}
