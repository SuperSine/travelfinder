namespace TravelfinderAPI.Agents;

public interface IModelFailover
{
    string? LastProviderUsed { get; }
}
