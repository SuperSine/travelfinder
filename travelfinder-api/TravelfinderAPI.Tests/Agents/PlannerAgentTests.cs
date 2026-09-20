using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPITests.Host;
using Xunit;

namespace TravelfinderAPITests.Agents;

public class PlannerAgentTests
{
    [Fact]
    public async Task Sparse_input_is_clarification_and_does_not_merge_places()
    {
        var places = new TrackingPlaceService();
        var chat = new ToolChatClient("""
            {"clarification":"How many days and what budget?","language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":[],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}
            """);
        var planner = new PlannerAgent(chat, places);

        var outcome = await planner.PlanAsync(
            [new ChatMessageDto { Role = "user", Content = "something fun" }],
            1.35, 103.82, "en-us", CancellationToken.None);

        Assert.True(outcome.IsClarification);
        Assert.Contains("days", outcome.Clarification!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(outcome.Spec);
        Assert.False(places.MergedCalled);
    }

    [Fact]
    public async Task Complete_input_returns_clamped_radius_and_allowed_categories()
    {
        var places = new TrackingPlaceService();
        var chat = new ToolChatClient("""
            {"clarification":"","language":"en-us","areaLabel":"Singapore","radiusMeters":99999,"categories":["park","spaceship","cafe"],"pointOfInterests":["fort canning"],"budgetLevel":"low","dayCount":2,"notes":"shade"}
            """);
        var planner = new PlannerAgent(chat, places);

        var outcome = await planner.PlanAsync(
            [new ChatMessageDto { Role = "user", Content = "two park days around Fort Canning, cheap, lots of shade" }],
            1.35, 103.82, "en-us", CancellationToken.None);

        Assert.False(outcome.IsClarification);
        Assert.Equal(20000, outcome.Spec!.RadiusMeters);
        Assert.Equal(["park", "cafe"], outcome.Spec.Categories);
        Assert.Equal(["fort canning"], outcome.Spec.PointOfInterests);
        Assert.Equal(BudgetLevel.Low, outcome.Spec.BudgetLevel);
        Assert.Equal(2, outcome.Spec.DayCount);
        Assert.False(places.MergedCalled);
    }
}
