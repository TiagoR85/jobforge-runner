using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace JobForge.Runner.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("redis unreachable", ex);
        }
    }
}
