using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Azure;
using Microsoft.Extensions.AI;
using System.ClientModel;

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
            try
            {
                LastProviderUsed = "xai";
                return await _xai.GetResponseAsync(materialized, options, cancellationToken);
            }
            catch (Exception xaiEx)
            {
                throw new ModelUnavailableException("Both planning models are unavailable.", xaiEx);
            }
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
            try
            {
                LastProviderUsed = "xai";
                source = _xai.GetStreamingResponseAsync(materialized, options, cancellationToken);
            }
            catch (Exception xaiEx)
            {
                throw new ModelUnavailableException("Both planning models are unavailable.", xaiEx);
            }
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

            if (current is RequestFailedException azure && IsFailoverStatusCode(azure.Status))
            {
                return true;
            }

            if (current is ClientResultException client && IsFailoverStatusCode(client.Status))
            {
                return true;
            }

            if (TryGetFailoverStatusCode(current, out var status) && IsFailoverStatusCode(status))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFailoverStatusCode(int status) =>
        status is 401 or 403 or 429 or >= 500;

    private static bool TryGetFailoverStatusCode(object exception, out int status)
    {
        status = 0;
        foreach (var propertyName in new[] { "Status", "StatusCode" })
        {
            var property = exception.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property?.GetValue(exception) is not { } value)
            {
                continue;
            }

            switch (value)
            {
                case int intStatus:
                    status = intStatus;
                    return true;
                case HttpStatusCode httpStatus:
                    status = (int)httpStatus;
                    return true;
            }
        }

        return false;
    }
}
