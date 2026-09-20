namespace TravelfinderAPI.Contracts;

public sealed class PlanRequest
{
    public List<ChatMessageDto> Messages { get; init; } = [];
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Language { get; init; }
    public required string RequestId { get; init; }
}
