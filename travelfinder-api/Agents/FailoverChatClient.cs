using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace TravelfinderAPI.Agents;

public sealed class FailoverChatClient : IChatClient, IModelFailover
{
    private readonly IChatClient _azure;
    private readonly IChatClient _xai;

    public FailoverChatClient(IChatClient azure, IChatClient xai)
    {
        _azure = azure;
        _xai = xai;
    }

    public string? LastProviderUsed { get; private set; } = "azure";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        try
        {
            LastProviderUsed = "azure";
            return await _azure.GetResponseAsync(materialized, options, cancellationToken);
        }
        catch (Exception ex) when (IsFailoverWorthy(ex) && !cancellationToken.IsCancellationRequested)
        {
            LastProviderUsed = "xai";
            return await _xai.GetResponseAsync(materialized, options, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        IAsyncEnumerable<ChatResponseUpdate> source;
        try
        {
            LastProviderUsed = "azure";
            source = _azure.GetStreamingResponseAsync(materialized, options, cancellationToken);
        }
        catch (Exception ex) when (IsFailoverWorthy(ex) && !cancellationToken.IsCancellationRequested)
        {
            LastProviderUsed = "xai";
            source = _xai.GetStreamingResponseAsync(materialized, options, cancellationToken);
        }

        await foreach (var update in source.WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IModelFailover) || serviceType == typeof(FailoverChatClient))
        {
            return this;
        }

        return _azure.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        _azure.Dispose();
        _xai.Dispose();
    }

    internal static bool IsFailoverWorthy(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is TaskCanceledException or TimeoutException or OperationCanceledException)
            {
                return current is not OperationCanceledException || current is TaskCanceledException;
            }

            if (current is HttpRequestException http &&
                http.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
            {
                return true;
            }
        }

        return false;
    }
}
