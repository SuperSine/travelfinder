namespace TravelfinderAPI.Contracts;

public sealed class ItineraryStop
{
    public int DayIndex { get; init; }
    public int StopIndex { get; init; }
    public required string PlaceId { get; init; }
    public required string Name { get; init; }
    public string? Reason { get; init; }
    public int? DurationMinutes { get; init; }
}
