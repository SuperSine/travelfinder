using System.Text.Json.Serialization;
using TravelfinderAPI.Host;

namespace TravelfinderAPI.Contracts;

[JsonConverter(typeof(LowercaseEnumConverter<BudgetLevel>))]
public enum BudgetLevel
{
    Low,
    Moderate,
    High
}

public sealed class PlanSpec
{
    public string Language { get; init; } = "en-us";
    public string AreaLabel { get; init; } = "current location";
    public int RadiusMeters { get; set; } = 5000;
    public string[] Categories { get; init; } = [];
    public string[] PointOfInterests { get; init; } = [];
    public BudgetLevel BudgetLevel { get; init; } = BudgetLevel.Moderate;
    public int DayCount { get; set; } = 1;
    public string Notes { get; init; } = "";

    public const int DefaultRadiusMeters = 5000;
    public const int MinRadiusMeters = 500;
    public const int MaxRadiusMeters = 20000;

    public void ClampRadius()
    {
        if (RadiusMeters <= 0)
        {
            RadiusMeters = DefaultRadiusMeters;
        }

        RadiusMeters = Math.Clamp(RadiusMeters, MinRadiusMeters, MaxRadiusMeters);
    }
}
