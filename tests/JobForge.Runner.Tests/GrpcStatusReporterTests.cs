using Grpc.Net.Client;
using JobForge.Orchestrator.Grpc;
using JobForge.Runner.Reporting;
using JobForge.Runner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobForge.Runner.Tests;

public class GrpcStatusReporterTests
{
    private static StatusReport Report(string id, string status) => new()
    {
        ExecutionId = id, Attempt = 1, Status = status, HttpStatus = 0, Error = "", DurationMs = 5
    };

    [Fact]
    public async Task Report_reaches_server_and_awaits_ack()
    {
        await using var server = await FakeStatusServer.StartAsync();
        using var channel = GrpcChannel.ForAddress(server.Url);
        await using var reporter = new GrpcStatusReporter(channel, NullLogger<GrpcStatusReporter>.Instance, TimeProvider.System);

        await reporter.ReportAsync(Report(Guid.NewGuid().ToString(), "running"), CancellationToken.None);

        await WaitUntil(() => server.Received.Count == 1, TimeSpan.FromSeconds(10));
        Assert.Single(server.Received);
    }

    [Fact]
    public async Task Failed_ack_is_requeued_and_retried()
    {
        await using var server = await FakeStatusServer.StartAsync();
        server.Persist = _ => false;
        using var channel = GrpcChannel.ForAddress(server.Url);
        await using var reporter = new GrpcStatusReporter(channel, NullLogger<GrpcStatusReporter>.Instance, TimeProvider.System);

        await reporter.ReportAsync(Report(Guid.NewGuid().ToString(), "failed"), CancellationToken.None);
        await WaitUntil(() => server.Received.Count >= 1, TimeSpan.FromSeconds(10));
        server.Persist = _ => true;
        await reporter.DrainPendingAsync(CancellationToken.None);

        await WaitUntil(() => server.Received.Count >= 2, TimeSpan.FromSeconds(10));
        Assert.True(server.Received.Count >= 2, $"expected >=2, got {server.Received.Count}");
    }

    [Fact]
    public async Task Reporter_survives_server_outage_and_reconnects()
    {
        await using var server = await FakeStatusServer.StartAsync();
        using var channel = GrpcChannel.ForAddress(server.Url);
        await using var reporter = new GrpcStatusReporter(channel, NullLogger<GrpcStatusReporter>.Instance, TimeProvider.System);

        await reporter.ReportAsync(Report(Guid.NewGuid().ToString(), "running"), CancellationToken.None);
        await WaitUntil(() => server.Received.Count >= 1, TimeSpan.FromSeconds(10));
        server.Down = true;
        await reporter.ReportAsync(Report(Guid.NewGuid().ToString(), "running"), CancellationToken.None);
        await Task.Delay(500);
        server.Down = false;
        await reporter.DrainPendingAsync(CancellationToken.None);

        await WaitUntil(() => server.Received.Count >= 2, TimeSpan.FromSeconds(15));
        Assert.True(server.Received.Count >= 2, $"expected >=2, got {server.Received.Count}");
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !condition())
            await Task.Delay(50);
    }
}
