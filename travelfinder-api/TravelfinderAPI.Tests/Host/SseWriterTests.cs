using System.Text;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPITests.Host;

public class SseWriterTests
{
    [Fact]
    public async Task Writes_event_name_and_closed_json_data()
    {
        var buffer = new MemoryStream();
        var writer = new StreamSseWriter(buffer);

        await writer.WriteAsync(PlanEventNames.PlanSpec, new PlanSpec { AreaLabel = "Singapore" }, CancellationToken.None);

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        Assert.Contains("event: plan_spec\n", text);
        Assert.Contains("data: {", text);
        Assert.Contains("\"areaLabel\":\"Singapore\"", text);
        Assert.EndsWith("\n\n", text);
    }
}
