using JobForge.Orchestrator.Grpc;
using JobForge.Runner.Configuration;
using JobForge.Runner.Dedup;
using JobForge.Runner.Domain;
using JobForge.Runner.Processing;
using JobForge.Runner.Reporting;
using JobForge.Runner.Tests.Fixtures;
using JobForge.Runner.Webhook;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobForge.Runner.Tests;

public class MessageProcessorTests
{
    private static DispatchEnvelope Envelope(int maxRetries = 3, int attempt = 0) => new(
        Guid.NewGuid(), Guid.NewGuid(), "http://example.local/hook",
        "{}", "POST", 5, maxRetries, attempt, Guid.NewGuid().ToString("N"));

    private sealed class CountingExecutor(WebhookResult result) : IWebhookExecutor
    {
        public int Calls { get; private set; }
        public Task<WebhookResult> ExecuteAsync(DispatchEnvelope envelope, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingDlq : IDeadLetterPublisher
    {
        public List<(DispatchEnvelope Env, int Attempt)> Published { get; } = [];
        public Task PublishAsync(DispatchEnvelope envelope, int finalAttempt, CancellationToken ct)
        {
            Published.Add((envelope, finalAttempt));
            return Task.CompletedTask;
        }
    }

    private sealed class OnceDedup : IDedup
    {
        public Task<bool> TryBeginAsync(Guid executionId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class NeverDedup : IDedup
    {
        public Task<bool> TryBeginAsync(Guid executionId, CancellationToken ct) => Task.FromResult(false);
    }

    private static MessageProcessor Processor(
        IDedup dedup, IWebhookExecutor exec, IStatusReporter reporter, RecordingDlq dlq,
        TimeProvider? clock = null, int maxBackoffSeconds = 30)
        => new(dedup, exec, reporter, dlq,
            clock ?? new FixedTimeProvider(DateTimeOffset.UtcNow),
            Options.Create(new RunnerOptions { MaxBackoffSeconds = maxBackoffSeconds }),
            NullLogger<MessageProcessor>.Instance);

    [Fact]
    public async Task Duplicate_is_skipped_without_execution_or_report()
    {
        var exec = new CountingExecutor(new WebhookResult(ExecutionOutcome.Succeeded, 200, null, 1));
        var reporter = new FakeStatusReporter();
        var dlq = new RecordingDlq();

        var result = await Processor(new NeverDedup(), exec, reporter, dlq).ProcessAsync(Envelope(), CancellationToken.None);

        Assert.Equal(ProcessResult.Skipped, result);
        Assert.Equal(0, exec.Calls);
        Assert.Empty(reporter.Reports);
        Assert.Empty(dlq.Published);
    }

    [Fact]
    public async Task Success_reports_running_then_succeeded()
    {
        var reporter = new FakeStatusReporter();
        var result = await Processor(new OnceDedup(),
                new CountingExecutor(new WebhookResult(ExecutionOutcome.Succeeded, 200, null, 12)),
                reporter, new RecordingDlq())
            .ProcessAsync(Envelope(), CancellationToken.None);

        Assert.Equal(ProcessResult.Succeeded, result);
        Assert.Equal(new[] { "running", "succeeded" }, reporter.Reports.Select(r => r.Status));
        Assert.Equal(200, reporter.Reports[^1].HttpStatus);
    }

    [Fact]
    public async Task Retryable_failures_backoff_then_succeed_on_last_attempt()
    {
        var reporter = new FakeStatusReporter();
        var exec = new SequencedExecutor(
            new WebhookResult(ExecutionOutcome.Retryable, 500, "boom", 5),
            new WebhookResult(ExecutionOutcome.Retryable, 500, "boom", 5),
            new WebhookResult(ExecutionOutcome.Succeeded, 200, null, 5));
        var delays = new List<TimeSpan>();
        var clock = new RecordingTimeProvider(delays);

        var result = await Processor(new OnceDedup(), exec, reporter, new RecordingDlq(), clock)
            .ProcessAsync(Envelope(maxRetries: 3), CancellationToken.None);

        Assert.Equal(ProcessResult.Succeeded, result);
        Assert.Equal(3, exec.Calls);
        Assert.Equal(new[] { "running", "failed", "failed", "succeeded" }, reporter.Reports.Select(r => r.Status));
        Assert.Equal(2, reporter.Reports.Count(r => r.Status == "failed"));
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, delays);
    }

    [Fact]
    public async Task Exhausted_retries_publish_dlq_and_report_dead()
    {
        var reporter = new FakeStatusReporter();
        var dlq = new RecordingDlq();
        var exec = new SequencedExecutor(
            new WebhookResult(ExecutionOutcome.Retryable, 503, "unavailable", 5),
            new WebhookResult(ExecutionOutcome.Retryable, 503, "unavailable", 5),
            new WebhookResult(ExecutionOutcome.Retryable, 503, "unavailable", 5));

        var result = await Processor(new OnceDedup(), exec, reporter, dlq,
                new FixedTimeProvider(DateTimeOffset.UtcNow))
            .ProcessAsync(Envelope(maxRetries: 3), CancellationToken.None);

        Assert.Equal(ProcessResult.Dead, result);
        Assert.Equal(3, exec.Calls);
        var dead = Assert.Single(dlq.Published);
        Assert.Equal(3, dead.Attempt);
        Assert.Equal("dead", reporter.Reports[^1].Status);
        Assert.Equal(3, reporter.Reports[^1].Attempt);
    }

    [Fact]
    public async Task Permanent_4xx_goes_straight_to_dlq_after_first_attempt()
    {
        var reporter = new FakeStatusReporter();
        var dlq = new RecordingDlq();
        var exec = new CountingExecutor(new WebhookResult(ExecutionOutcome.Permanent, 404, "HTTP 404", 5));

        var result = await Processor(new OnceDedup(), exec, reporter, dlq,
                new FixedTimeProvider(DateTimeOffset.UtcNow))
            .ProcessAsync(Envelope(maxRetries: 5), CancellationToken.None);

        Assert.Equal(ProcessResult.Dead, result);
        Assert.Equal(1, exec.Calls);
        var dead = Assert.Single(dlq.Published);
        Assert.Equal(1, dead.Attempt);
    }

    private sealed class SequencedExecutor(params WebhookResult[] results) : IWebhookExecutor
    {
        private int _i;
        public int Calls { get; private set; }
        public Task<WebhookResult> ExecuteAsync(DispatchEnvelope envelope, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(results[Math.Min(_i++, results.Length - 1)]);
        }
    }

    private sealed class RecordingTimeProvider(List<TimeSpan> delays) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public override System.Threading.ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            delays.Add(dueTime);
            _ = Task.Run(() => callback(state));
            return new NoopTimer();
        }

        private sealed class NoopTimer : System.Threading.ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
