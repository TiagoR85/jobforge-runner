using StackExchange.Redis;

namespace JobForge.Runner.Dedup;

public interface IDedup
{
    Task<bool> TryBeginAsync(Guid executionId, CancellationToken ct);
}

public sealed class RedisDedup(IConnectionMultiplexer redis, int ttlSeconds) : IDedup
{
    public async Task<bool> TryBeginAsync(Guid executionId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        return await db.StringSetAsync($"runner:exec:{executionId}", "1",
            TimeSpan.FromSeconds(ttlSeconds), When.NotExists);
    }
}
