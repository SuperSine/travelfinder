using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Host;

namespace TravelfinderAPI.Agents;

public sealed class PlannerAgent : IPlanner
{
    private const string DefaultClarification = "I need a bit more detail about the trip.";

    private const string PlannerInstructions = """
        You are a travel planning assistant. Convert the user's request into a structured plan or ask one clarification question.
        Do not invent latitude/longitude coordinates or output a concrete list of places.
        Use only categories from the allowed set: park, restaurant, art_gallery, museum, historical_landmark, cafe, bar, library, night_club, store, jewelry_store.
        When day count, area, or interests are missing, ask one clarification question via the clarification field.
        Always respond by calling emit_plan_result with your result.
        """;

    private readonly AIAgent _agent;
    private readonly IPlaceService _places;

    public PlannerAgent(IChatClient chatClient, IPlaceService places)
    {
        _places = places;
        _agent = chatClient.AsAIAgent(instructions: PlannerInstructions, name: "planner");
    }

    public async Task<PlannerOutcome> PlanAsync(
        IReadOnlyList<ChatMessageDto> messages,
        double latitude,
        double longitude,
        string language,
        CancellationToken cancellationToken)
    {
        var areaLabel = await _places.ReverseGeocode(latitude, longitude, cancellationToken);
        if (string.IsNullOrWhiteSpace(areaLabel))
        {
            areaLabel = "current location";
        }

        var chatMessages = new List<ChatMessage>(messages.Count + 1);
        foreach (var message in messages)
        {
            chatMessages.Add(new ChatMessage(MapRole(message.Role), message.Content));
        }

        chatMessages.Add(new ChatMessage(
            ChatRole.User,
            $"User coordinates are for reverse-geocode context only. Area label: {areaLabel}."));

        var response = await _agent.RunAsync(chatMessages, cancellationToken: cancellationToken);
        var result = ParseResult(ExtractResultJson(response));
        if (result is null)
        {
            return new PlannerOutcome { Clarification = DefaultClarification };
        }

        if (!string.IsNullOrWhiteSpace(result.Clarification))
        {
            return new PlannerOutcome { Clarification = result.Clarification };
        }

        var spec = new PlanSpec
        {
            Language = string.IsNullOrWhiteSpace(result.Language) ? language : result.Language,
            AreaLabel = areaLabel,
            RadiusMeters = result.RadiusMeters,
            Categories = AllowedGoogleTypes.Clip(result.Categories),
            PointOfInterests = result.PointOfInterests,
            BudgetLevel = ParseBudgetLevel(result.BudgetLevel),
            DayCount = result.DayCount < 1 ? 1 : result.DayCount,
            Notes = result.Notes ?? ""
        };
        spec.ClampRadius();

        return new PlannerOutcome { Spec = spec };
    }

    private static string? ExtractResultJson(AgentResponse response)
    {
        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent call &&
                    string.Equals(call.Name, "emit_plan_result", StringComparison.OrdinalIgnoreCase) &&
                    call.Arguments is not null)
                {
                    return call.Arguments.ToString();
                }
            }
        }

        var text = response.Text;
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static PlanResultJson? ParseResult(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlanResultJson>(json, PlanJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BudgetLevel ParseBudgetLevel(string? value) =>
        Enum.TryParse<BudgetLevel>(value, ignoreCase: true, out var level)
            ? level
            : BudgetLevel.Moderate;

    private static ChatRole MapRole(string role) => role.ToLowerInvariant() switch
    {
        "system" => ChatRole.System,
        "assistant" => ChatRole.Assistant,
        "user" => ChatRole.User,
        _ => ChatRole.User
    };

    private sealed class PlanResultJson
    {
        public string? Clarification { get; init; }
        public string? Language { get; init; }
        public string? AreaLabel { get; init; }
        public int RadiusMeters { get; init; }
        public string[] Categories { get; init; } = [];
        public string[] PointOfInterests { get; init; } = [];
        public string? BudgetLevel { get; init; }
        public int DayCount { get; init; }
        public string? Notes { get; init; }
    }
}
