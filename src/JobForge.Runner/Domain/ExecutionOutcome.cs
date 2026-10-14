namespace JobForge.Runner.Domain;

public enum ExecutionOutcome
{
    Succeeded,
    Retryable,
    Permanent
}

public sealed record WebhookResult(ExecutionOutcome Outcome, int? HttpStatus, string? Error, long DurationMs);
