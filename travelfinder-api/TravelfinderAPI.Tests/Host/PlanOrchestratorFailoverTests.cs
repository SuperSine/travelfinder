using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using TravelfinderAPITests.Agents;
using Xunit;

namespace TravelfinderAPITests.Host;

public class PlanOrchestratorFailoverTests
{
    [Fact]
    public async Task Done_includes_providerUsed_from_failover_client()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("429", null, System.Net.HttpStatusCode.TooManyRequests));
        var xai = new ToolChatClient("""
            {"clarification":"Which neighborhood?","language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":[],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}
            """);
        var failover = new FailoverChatClient(azure, xai);
        var planner = new PlannerAgent(failover, new TrackingPlaceService());
        var orchestrator = new PlanOrchestrator(
            planner,
            new FakeRenderer([]),
            new TrackingPlaceService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanOrchestrator>.Instance,
            failover);

        var writer = new RecordingSseWriter();
        await orchestrator.RunAsync(new PlanRequest
        {
            RequestId = "r1",
            Messages = [new ChatMessageDto { Role = "user", Content = "hi" }],
            Latitude = 1.35,
            Longitude = 103.82
        }, writer, CancellationToken.None);

        Assert.Equal(PlanEventNames.Done, writer.Names[^1]);
        Assert.Contains("\"providerUsed\":\"xai\"", writer.Data[^1]);
        Assert.Equal(1, azure.Calls);
    }
}
