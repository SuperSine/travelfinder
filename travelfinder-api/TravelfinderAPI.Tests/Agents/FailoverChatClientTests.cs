using System.Net;
using Azure;
using Microsoft.Extensions.AI;
using TravelfinderAPI.Agents;
using Xunit;

namespace TravelfinderAPITests.Agents;

public class FailoverChatClientTests
{
    [Fact]
    public async Task Azure_429_retries_xai_exactly_once()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("throttled", null, HttpStatusCode.TooManyRequests));
        var xai = new StubChatClient("xai", response: "ok");
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(1, azure.Calls);
        Assert.Equal(1, xai.Calls);
        Assert.Equal("xai", client.LastProviderUsed);
    }

    [Fact]
    public async Task Azure_success_does_not_call_xai()
    {
        var azure = new StubChatClient("azure", response: "from-azure");
        var xai = new StubChatClient("xai", response: "from-xai");
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("from-azure", response.Text);
        Assert.Equal(1, azure.Calls);
        Assert.Equal(0, xai.Calls);
        Assert.Equal("azure", client.LastProviderUsed);
    }

    [Fact]
    public async Task Both_models_fail_throws_ModelUnavailableException()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("down", null, HttpStatusCode.BadGateway));
        var xai = new StubChatClient("xai", new HttpRequestException("down", null, HttpStatusCode.ServiceUnavailable));
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        await Assert.ThrowsAsync<ModelUnavailableException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.Equal(1, azure.Calls);
        Assert.Equal(1, xai.Calls);
    }

    [Fact]
    public async Task Azure_non_http_429_failover_calls_xai_once()
    {
        var azure = new StubChatClient("azure", new StatusStubException(429));
        var xai = new StubChatClient("xai", response: "ok");
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(1, azure.Calls);
        Assert.Equal(1, xai.Calls);
    }

    [Fact]
    public async Task Azure_401_failover_calls_xai()
    {
        var azure = new StubChatClient("azure", new RequestFailedException(401, "unauthorized"));
        var xai = new StubChatClient("xai", response: "ok");
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(1, xai.Calls);
    }

    [Fact]
    public async Task Argument_exception_does_not_failover()
    {
        var azure = new StubChatClient("azure", new ArgumentException("bad prompt"));
        var xai = new StubChatClient("xai", response: "should-not-run");
        var client = new TravelfinderAPI.Agents.FailoverChatClient(azure, xai);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.Equal(0, xai.Calls);
    }

    private sealed class StatusStubException : Exception
    {
        public StatusStubException(int status) => Status = status;
        public int Status { get; }
    }
}
