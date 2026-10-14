using JobForge.Orchestrator.Grpc;
using JobForge.Runner.Configuration;
using JobForge.Runner.Dedup;
using JobForge.Runner.Domain;
using JobForge.Runner.Reporting;
using JobForge.Runner.Webhook;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobForge.Runner.Processing;

public enum ProcessResult
{
    Skipped,
    Succeeded,
    Dead
}

public sealed class MessageProcessor(
    IDedup dedup,
    IWebhookExecutor executor,
    IStatusReporter reporter,
    IDeadLetterPublisher deadLetter,
    TimeProvider clock,
    IOptions<RunnerOptions> options,
    ILogger<MessageProcessor> logger)
{
    public async Task<ProcessResult> ProcessAsync(DispatchEnvelope envelope, CancellationToken ct)
    {
        if (!await dedup.TryBeginAsync(envelope.ExecutionId, ct))
        {
            logger.LogInformation("Duplicate delivery for {ExecutionId}; skipping", envelope.ExecutionId);
            return ProcessResult.Skipped;
        }

        await reporter.ReportAsync(new StatusReport
        {
            ExecutionId = envelope.ExecutionId.ToString(),
            Attempt = 1,
            Status = "running",
            Error = "",
            DurationMs = 0
        }, ct);

        var attempt = 0;
        while (true)
        {
            attempt++;
            var result = await executor.ExecuteAsync(envelope, ct);

            switch (result.Outcome)
            {
                case ExecutionOutcome.Succeeded:
                    await reporter.ReportAsync(new StatusReport
                    {
                        ExecutionId = envelope.ExecutionId.ToString(),
                        Attempt = attempt,
                        Status = "succeeded",
                        HttpStatus = result.HttpStatus ?? 0,
                        Error = result.Error ?? "",
                        DurationMs = result.DurationMs
                    }, ct);
                    return ProcessResult.Succeeded;

                case ExecutionOutcome.Permanent:
                    await deadLetter.PublishAsync(envelope, attempt, ct);
                    await ReportDeadAsync(envelope, attempt, result, ct);
                    return ProcessResult.Dead;

                case ExecutionOutcome.Retryable:
                    if (attempt >= Math.Max(1, envelope.MaxRetries))
                    {
                        await deadLetter.PublishAsync(envelope, attempt, ct);
                        await ReportDeadAsync(envelope, attempt, result, ct);
                        return ProcessResult.Dead;
                    }
                    await reporter.ReportAsync(new StatusReport
                    {
                        ExecutionId = envelope.ExecutionId.ToString(),
                        Attempt = attempt,
                        Status = "failed",
                        HttpStatus = result.HttpStatus ?? 0,
                        Error = result.Error ?? "",
                        DurationMs = result.DurationMs
                    }, ct);
                    var delaySeconds = Math.Min(1L << (attempt - 1), options.Value.MaxBackoffSeconds);
                    logger.LogInformation(
                        "Attempt {Attempt}/{Max} for {ExecutionId} failed ({Error}); retrying in {Delay}s",
                        attempt, envelope.MaxRetries, envelope.ExecutionId, result.Error, delaySeconds);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), clock, ct);
                    break;
            }
        }
    }

    private Task ReportDeadAsync(DispatchEnvelope envelope, int attempt, WebhookResult result, CancellationToken ct)
        => reporter.ReportAsync(new StatusReport
        {
            ExecutionId = envelope.ExecutionId.ToString(),
            Attempt = attempt,
            Status = "dead",
            HttpStatus = result.HttpStatus ?? 0,
            Error = result.Error ?? "",
            DurationMs = result.DurationMs
        }, ct);
}
