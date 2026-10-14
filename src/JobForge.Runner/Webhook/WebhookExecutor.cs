using System.Diagnostics;
using System.Net;
using JobForge.Runner.Domain;

namespace JobForge.Runner.Webhook;

public sealed class WebhookExecutor(HttpClient client) : IWebhookExecutor
{
    public async Task<WebhookResult> ExecuteAsync(DispatchEnvelope envelope, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(
                new HttpMethod(envelope.Method), envelope.WebhookUrl);
            if (envelope.Method is not "GET" && !string.IsNullOrEmpty(envelope.Payload))
                request.Content = new StringContent(envelope.Payload, System.Text.Encoding.UTF8, "application/json");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, envelope.TimeoutSeconds)));
            using var response = await client.SendAsync(request, timeoutCts.Token);
            stopwatch.Stop();

            var code = (int)response.StatusCode;
            var outcome = Classify(code);
            return new WebhookResult(outcome, code, outcome == ExecutionOutcome.Succeeded ? null : $"HTTP {code}", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new WebhookResult(ExecutionOutcome.Retryable, null, $"webhook timed out after {envelope.TimeoutSeconds}s", stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new WebhookResult(ExecutionOutcome.Retryable, null, ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    internal static ExecutionOutcome Classify(int statusCode) => statusCode switch
    {
        >= 200 and < 400 => ExecutionOutcome.Succeeded,
        408 or 429 => ExecutionOutcome.Retryable,
        >= 400 and < 500 => ExecutionOutcome.Permanent,
        _ => ExecutionOutcome.Retryable
    };
}

public interface IWebhookExecutor
{
    Task<WebhookResult> ExecuteAsync(DispatchEnvelope envelope, CancellationToken ct);
}
