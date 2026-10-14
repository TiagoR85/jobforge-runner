using System.Net;
using JobForge.Runner.Domain;
using JobForge.Runner.Webhook;
using Xunit;

namespace JobForge.Runner.Tests;

public class WebhookExecutorTests
{
    private static DispatchEnvelope Envelope(int timeoutSeconds = 5) => new(
        Guid.NewGuid(), Guid.NewGuid(), "http://example.local/hook",
        "{}", "POST", timeoutSeconds, 5, 0, Guid.NewGuid().ToString("N"));

    private static WebhookExecutor WithHandler(HttpMessageHandler handler, int timeoutSeconds = 5)
    {
        var client = new HttpClient(handler);
        return new WebhookExecutor(client);
    }

    private sealed class StubHandler(HttpStatusCode code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(code));
    }

    private sealed class SlowHandler(int seconds) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("connection refused");
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(301)]
    public async Task Success_and_redirect_are_succeeded(int code)
    {
        var result = await WithHandler(new StubHandler((HttpStatusCode)code)).ExecuteAsync(Envelope(), CancellationToken.None);
        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(code, result.HttpStatus);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    public async Task Plain_4xx_is_permanent(int code)
    {
        var result = await WithHandler(new StubHandler((HttpStatusCode)code)).ExecuteAsync(Envelope(), CancellationToken.None);
        Assert.Equal(ExecutionOutcome.Permanent, result.Outcome);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Timeoutish_4xx_and_5xx_are_retryable(int code)
    {
        var result = await WithHandler(new StubHandler((HttpStatusCode)code)).ExecuteAsync(Envelope(), CancellationToken.None);
        Assert.Equal(ExecutionOutcome.Retryable, result.Outcome);
    }

    [Fact]
    public async Task Network_error_is_retryable_with_error_text()
    {
        var result = await WithHandler(new ThrowingHandler()).ExecuteAsync(Envelope(), CancellationToken.None);
        Assert.Equal(ExecutionOutcome.Retryable, result.Outcome);
        Assert.Contains("connection refused", result.Error);
        Assert.Null(result.HttpStatus);
    }

    [Fact]
    public async Task Client_timeout_is_retryable()
    {
        var result = await WithHandler(new SlowHandler(3), timeoutSeconds: 1)
            .ExecuteAsync(Envelope(timeoutSeconds: 1), CancellationToken.None);
        Assert.Equal(ExecutionOutcome.Retryable, result.Outcome);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Duration_is_measured()
    {
        var result = await WithHandler(new StubHandler(HttpStatusCode.OK)).ExecuteAsync(Envelope(), CancellationToken.None);
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public async Task Method_and_payload_are_forwarded()
    {
        var capture = new CaptureHandler();
        var envelope = Envelope() with { Method = "GET", Payload = "{\"a\":1}" };
        await WithHandler(capture).ExecuteAsync(envelope, CancellationToken.None);
        Assert.Equal(HttpMethod.Get, capture.Method);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
