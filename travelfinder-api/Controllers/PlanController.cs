using Microsoft.AspNetCore.Mvc;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;

namespace TravelfinderAPI.Controllers;

[ApiController]
[Route("plan")]
public sealed class PlanController : ControllerBase
{
    private readonly PlanOrchestrator _orchestrator;

    public PlanController(PlanOrchestrator orchestrator) => _orchestrator = orchestrator;

    [HttpPost("stream")]
    public async Task Stream([FromBody] PlanRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("Connection", "keep-alive");
        var writer = new StreamSseWriter(Response.Body);
        await _orchestrator.RunAsync(request, writer, cancellationToken);
    }
}
