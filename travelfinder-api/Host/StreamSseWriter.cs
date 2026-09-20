using System.Text;
using System.Text.Json;

namespace TravelfinderAPI.Host;

public sealed class StreamSseWriter : ISseWriter
{
    private readonly Stream _stream;

    public StreamSseWriter(Stream stream) => _stream = stream;

    public async Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, payload.GetType(), PlanJson.Options);
        var frame = $"event: {eventName}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _stream.WriteAsync(bytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }
}
