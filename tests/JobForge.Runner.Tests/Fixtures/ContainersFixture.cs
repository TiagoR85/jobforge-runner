using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;
using Testcontainers.Redis;
using Xunit;

namespace JobForge.Runner.Tests.Fixtures;

public sealed class ContainersFixture : IAsyncLifetime
{
    public static readonly string[] Topics = ["jobs.dispatch", "jobs.dead"];

    public RedisContainer Redis { get; } = new RedisBuilder("redis:7-alpine").Build();
    public KafkaContainer Kafka { get; } = new KafkaBuilder("confluentinc/cp-kafka:7.9.0").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(Redis.StartAsync(), Kafka.StartAsync());
        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = Kafka.GetBootstrapAddress() }).Build();
        var specs = Topics.Select(t => new TopicSpecification
        {
            Name = t, NumPartitions = 3, ReplicationFactor = 1
        }).ToArray();
        try { await admin.CreateTopicsAsync(specs); }
        catch (CreateTopicsException ex) when (
            ex.Results.All(r => r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError)) { }
    }

    public async Task DisposeAsync()
        => await Task.WhenAll(Redis.DisposeAsync().AsTask(), Kafka.DisposeAsync().AsTask());
}

[CollectionDefinition("containers")]
public sealed class ContainersCollection : ICollectionFixture<ContainersFixture> { }
