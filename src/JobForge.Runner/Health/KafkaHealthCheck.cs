using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JobForge.Runner.Health;

public sealed class KafkaHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"]
            }).Build();
            _ = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("kafka unreachable", ex));
        }
    }
}
