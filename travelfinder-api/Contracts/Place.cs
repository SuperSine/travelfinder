using System.Text.Json.Serialization;
using TravelfinderAPI.Host;

namespace TravelfinderAPI.Contracts;

[JsonConverter(typeof(LowercaseEnumConverter<PlaceSource>))]
public enum PlaceSource
{
    Google,
    Arcgis
}

public sealed class Place
{
    public required string Id { get; init; }
    public required PlaceSource Source { get; init; }
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
    public string? PrimaryType { get; init; }
    public string[] Categories { get; init; } = [];
    public double? Rating { get; init; }
    public string? PriceLevel { get; init; }
    public required GeoPoint Location { get; init; }
    public double Score { get; set; }

    public static string ComposeId(PlaceSource source, string sourceId) =>
        source == PlaceSource.Google ? $"google:{sourceId}" : $"arcgis:{sourceId}";
}
