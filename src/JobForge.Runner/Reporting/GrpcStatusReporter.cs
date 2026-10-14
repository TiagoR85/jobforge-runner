using System.Threading.Channels;
using Grpc.Net.Client;
using JobForge.Orchestrator.Grpc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobForge.Runner.Reporting;

public sealed class GrpcStatusReporter : BackgroundService, IStatusReporter, IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ILogger<GrpcStatusReporter> _logger;
    private readonly TimeProvider _clock;
    private readonly Channel<StatusReport> _pending = Channel.CreateUnbounded<StatusReport>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;

    public GrpcStatusReporter(GrpcChannel channel, ILogger<GrpcStatusReporter> logger, TimeProvider clock)
    {
        _channel = channel;
        _logger = logger;
        _clock = clock;
        _pump = Task.Run(PumpAsync);
    }

    public async Task ReportAsync(StatusReport report, CancellationToken ct)
        => await _pending.Writer.WriteAsync(report, ct);

    public async Task DrainPendingAsync(CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var last = -1;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, ct);
            var count = _pending.Reader.Count;
            if (count == 0 && last == 0) return;
            last = count;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), _cts);
        try { await _pump; }
        catch (OperationCanceledException) { }
    }

    private async Task PumpAsync()
    {
        var backoffSeconds = 1;
        while (!_cts.IsCancellationRequested)
        {
            StatusReport? current = null;
            try
            {
                var client = new ExecutionStatus.ExecutionStatusClient(_channel);
                using var call = client.Report(cancellationToken: _cts.Token);
                backoffSeconds = 1;

                while (await _pending.Reader.WaitToReadAsync(_cts.Token))
                {
                    while (_pending.Reader.TryRead(out var report))
                    {
                        current = report;
                        await call.RequestStream.WriteAsync(report, _cts.Token);

                        if (!await call.ResponseStream.MoveNext(_cts.Token))
                            throw new InvalidOperationException("status stream ended without ack");

                        var ack = call.ResponseStream.Current;
                        if (!ack.Persisted)
                            throw new InvalidOperationException($"report for {ack.ExecutionId} not persisted");

                        current = null;
                    }
                }

                await Task.Delay(System.Threading.Timeout.Infinite, _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "gRPC status stream failed; reconnecting in {Seconds}s", backoffSeconds);
            }
            finally
            {
                if (current is not null) _pending.Writer.TryWrite(current);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), _clock, _cts.Token); }
            catch (OperationCanceledException) { break; }
            backoffSeconds = Math.Min(backoffSeconds * 2, 30);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        await base.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _pending.Writer.TryComplete();
        await _cts.CancelAsync();
        try { await _pump; } catch (OperationCanceledException) { }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
