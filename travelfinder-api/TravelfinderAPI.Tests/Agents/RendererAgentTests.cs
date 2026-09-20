using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using Xunit;

namespace TravelfinderAPITests.Agents;

public class RendererAgentTests
{
    [Fact]
    public async Task Writes_stops_only_from_provided_places()
    {
        var chat = new ToolChatClient("""
            [
              {"dayIndex":0,"stopIndex":0,"placeId":"google:1","name":"Fort Canning","reason":"Start","durationMinutes":90},
              {"dayIndex":0,"stopIndex":1,"placeId":"invented:99","name":"Fake Pier","reason":"Nope","durationMinutes":30}
            ]
            """);
        var renderer = new RendererAgent(chat);
        var places = new[]
        {
            new Place
            {
                Id = "google:1",
                Source = PlaceSource.Google,
                SourceId = "1",
                Name = "Fort Canning",
                Categories = ["park"],
                Location = new GeoPoint(1.295, 103.846)
            }
        };

        var stops = new List<ItineraryStop>();
        await foreach (var stop in renderer.RenderAsync(new PlanSpec { DayCount = 1 }, places, CancellationToken.None))
        {
            stops.Add(stop);
        }

        Assert.Single(stops);
        Assert.Equal("google:1", stops[0].PlaceId);
        Assert.Equal(0, stops[0].DayIndex);
        Assert.Equal(0, stops[0].StopIndex);
    }
}
