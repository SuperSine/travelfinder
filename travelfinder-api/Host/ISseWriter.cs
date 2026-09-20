namespace TravelfinderAPI.Host;

public interface ISseWriter
{
    Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken);
}
