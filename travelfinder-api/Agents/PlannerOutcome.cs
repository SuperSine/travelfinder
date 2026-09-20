using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Agents;

public sealed class PlannerOutcome
{
    public PlanSpec? Spec { get; init; }
    public string? Clarification { get; init; }
    public bool IsClarification => !string.IsNullOrWhiteSpace(Clarification);
}
