using JobForge.Runner.Dedup;
using JobForge.Runner.Tests.Fixtures;
using StackExchange.Redis;
using Xunit;

namespace JobForge.Runner.Tests;

[Collection("containers")]
public class RedisDedupTests
{
    private readonly ContainersFixture _infra;
    public RedisDedupTests(ContainersFixture infra) => _infra = infra;

    [Fact]
    public async Task First_call_wins_second_is_duplicate()
    {
        await using var redis = ConnectionMultiplexer.Connect(_infra.Redis.GetConnectionString());
        var dedup = new RedisDedup(redis, ttlSeconds: 60);
        var id = Guid.NewGuid();

        Assert.True(await dedup.TryBeginAsync(id, CancellationToken.None));
        Assert.False(await dedup.TryBeginAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task Different_executions_are_independent()
    {
        await using var redis = ConnectionMultiplexer.Connect(_infra.Redis.GetConnectionString());
        var dedup = new RedisDedup(redis, ttlSeconds: 60);

        Assert.True(await dedup.TryBeginAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.True(await dedup.TryBeginAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Expired_key_allows_reprocessing()
    {
        await using var redis = ConnectionMultiplexer.Connect(_infra.Redis.GetConnectionString());
        var dedup = new RedisDedup(redis, ttlSeconds: 1);
        var id = Guid.NewGuid();

        Assert.True(await dedup.TryBeginAsync(id, CancellationToken.None));
        await Task.Delay(1200);
        Assert.True(await dedup.TryBeginAsync(id, CancellationToken.None));
    }
}
