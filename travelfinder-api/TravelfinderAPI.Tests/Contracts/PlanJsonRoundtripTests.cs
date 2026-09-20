using System.Text.Json;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPITests.Contracts;

public class PlanJsonRoundtripTests
{
    [Fact]
    public void Place_serializes_id_as_source_colon_sourceId()
    {
        var place = new Place
        {
            Id = "google:abc",
            Source = PlaceSource.Google,
            SourceId = "abc",
            Name = "Fort Canning",
            Address = "River Valley Rd",
            PrimaryType = "park",
            Categories = ["park"],
            Rating = 4.6,
            PriceLevel = "PRICE_LEVEL_MODERATE",
            Location = new GeoPoint(1.295, 103.846),
            Score = 0.81
        };

        var json = JsonSerializer.Serialize(place, PlanJson.Options);

        Assert.Contains("\"id\":\"google:abc\"", json);
        Assert.Contains("\"source\":\"google\"", json);
        Assert.Contains("\"sourceId\":\"abc\"", json);
        Assert.Contains("\"latitude\":1.295", json);
    }

    [Fact]
    public void PlanRequest_does_not_accept_systemId_property()
    {
        const string body = """
            {"messages":[{"role":"user","content":"coffee"}],"latitude":1.35,"longitude":103.82,"requestId":"r1","systemId":"gis_helper"}
            """;

        var request = JsonSerializer.Deserialize<PlanRequest>(body, PlanJson.Options);

        Assert.NotNull(request);
        Assert.Equal("r1", request!.RequestId);
        Assert.Null(request.GetType().GetProperty("SystemId"));
    }
}
