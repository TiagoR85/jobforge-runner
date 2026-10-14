using JobForge.Runner.Health;
using JobForge.Runner.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;

namespace JobForge.Runner.Tests;

[Collection("containers")]
public class HealthCheckTests
{
    private readonly ContainersFixture _infra;
    public HealthCheckTests(ContainersFixture infra) => _infra = infra;

    [Fact]
    public async Task Redis_health_check_is_healthy_against_real_redis()
    {
        await using var redis = ConnectionMultiplexer.Connect(_infra.Redis.GetConnectionString());
        var check = new RedisHealthCheck(redis);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Kafka_health_check_is_healthy_against_real_kafka()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = _infra.Kafka.GetBootstrapAddress()
            })
            .Build();
        var check = new KafkaHealthCheck(config);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Redis_health_check_is_unhealthy_on_bad_connection()
    {
        var options = new ConfigurationOptions { AbortOnConnectFail = false, ConnectTimeout = 200 };
        options.EndPoints.Add("127.0.0.1", 1);
        await using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        var check = new RedisHealthCheck(redis);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Kafka_health_check_is_unhealthy_on_bad_bootstrap()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "127.0.0.1:1"
            })
            .Build();
        var check = new KafkaHealthCheck(config);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy, result.Status);
    }
}
