namespace TravelfinderAPI.Host;

public sealed class HttpSseWriter : ISseWriter
{
    private readonly StreamSseWriter _inner;

    public HttpSseWriter(HttpResponse response)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("Connection", "keep-alive");
        _inner = new StreamSseWriter(response.Body);
    }

    public Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        _inner.WriteAsync(eventName, payload, cancellationToken);
}
