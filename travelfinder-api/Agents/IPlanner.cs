using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Agents;

public interface IPlanner
{
    Task<PlannerOutcome> PlanAsync(
        IReadOnlyList<ChatMessageDto> messages,
        double latitude,
        double longitude,
        string language,
        CancellationToken cancellationToken);
}
