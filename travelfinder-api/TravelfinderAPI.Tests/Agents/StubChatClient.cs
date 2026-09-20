using Microsoft.Extensions.AI;

namespace TravelfinderAPITests.Agents;

internal sealed class StubChatClient : IChatClient
{
    public StubChatClient(string name, Exception? error = null, string response = "")
    {
        Name = name;
        Error = error;
        Response = response;
    }

    public string Name { get; }
    public Exception? Error { get; }
    public string Response { get; }
    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Error is not null) throw Error;
        return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, Response)]));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Error is not null) throw Error;
        yield return new ChatResponseUpdate(ChatRole.Assistant, Response);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
