using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;

namespace TravelfinderAPI.Agents;

public sealed class RendererAgent : IRenderer
{
    private const string RendererInstructions = """
        You are an itinerary renderer. Turn the plan and place list into a JSON array of stops.
        Only use place ids from the supplied place list. Do not invent places or coordinates.
        Each stop must include dayIndex (0-based, less than the plan day count), stopIndex (0-based per day), placeId, name, reason, and durationMinutes.
        Respond with a JSON array of stops only.
        """;

    private readonly IChatClient _chatClient;
    private readonly AIAgent _agent;

    public RendererAgent(IChatClient chatClient)
    {
        _chatClient = chatClient;
        _agent = chatClient.AsAIAgent(instructions: RendererInstructions, name: "renderer");
    }

    public async IAsyncEnumerable<ItineraryStop> RenderAsync(
        PlanSpec spec,
        IReadOnlyList<Contracts.Place> places,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var knownIds = places.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var userMessage = BuildUserMessage(spec, places);
        var json = await GetResponseJsonAsync(userMessage, cancellationToken);

        foreach (var stop in ParseStops(json)
            .Where(s => knownIds.Contains(s.PlaceId))
            .OrderBy(s => s.DayIndex)
            .ThenBy(s => s.StopIndex))
        {
            yield return stop;
        }
    }

    private async Task<string> GetResponseJsonAsync(string userMessage, CancellationToken cancellationToken)
    {
        var response = await _agent.RunAsync(
            [new ChatMessage(ChatRole.User, userMessage)],
            cancellationToken: cancellationToken);
        var text = response.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var fallback = await _chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, RendererInstructions),
                new ChatMessage(ChatRole.User, userMessage)
            ],
            cancellationToken: cancellationToken);
        return fallback.Text ?? "";
    }

    private static string BuildUserMessage(PlanSpec spec, IReadOnlyList<Contracts.Place> places)
    {
        var payload = new RendererRequest
        {
            Spec = spec,
            Places = places.Select(p => new PlaceSummary
            {
                Id = p.Id,
                Name = p.Name,
                PrimaryType = p.PrimaryType,
                Address = p.Address,
                Score = p.Score
            }).ToArray()
        };

        return JsonSerializer.Serialize(payload, PlanJson.Options);
    }

    private static IReadOnlyList<ItineraryStop> ParseStops(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<ItineraryStop[]>(json.Trim(), PlanJson.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class RendererRequest
    {
        public required PlanSpec Spec { get; init; }
        public required PlaceSummary[] Places { get; init; }
    }

    private sealed class PlaceSummary
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? PrimaryType { get; init; }
        public string? Address { get; init; }
        public double Score { get; init; }
    }
}
