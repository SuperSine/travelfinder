using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Agents;

public interface IRenderer
{
    IAsyncEnumerable<ItineraryStop> RenderAsync(
        PlanSpec spec,
        IReadOnlyList<Contracts.Place> places,
        CancellationToken cancellationToken);
}
