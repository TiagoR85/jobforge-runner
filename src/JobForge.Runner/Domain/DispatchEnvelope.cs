using System.Text.Json;

namespace JobForge.Runner.Domain;

public sealed record DispatchEnvelope(
    Guid ExecutionId,
    Guid JobId,
    string WebhookUrl,
    string Payload,
    string Method,
    int TimeoutSeconds,
    int MaxRetries,
    int Attempt,
    string TraceId)
{
    public static DispatchEnvelope? TryParse(string json)
    {
        try
        {
            var e = JsonSerializer.Deserialize<DispatchEnvelope>(json);
            return e is not null && e.ExecutionId != Guid.Empty ? e : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
