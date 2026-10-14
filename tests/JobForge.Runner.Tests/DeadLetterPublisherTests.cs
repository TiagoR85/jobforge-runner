using System.Text.Json;
using Confluent.Kafka;
using JobForge.Runner.Domain;
using JobForge.Runner.Processing;
using JobForge.Runner.Tests.Fixtures;
using Xunit;

namespace JobForge.Runner.Tests;

[Collection("containers")]
public class DeadLetterPublisherTests
{
    private readonly ContainersFixture _infra;
    public DeadLetterPublisherTests(ContainersFixture infra) => _infra = infra;

    [Fact]
    public async Task Publish_ssembles_message_with_key_header_and_payload()
    {
        var envelope = new DispatchEnvelope(Guid.NewGuid(), Guid.NewGuid(),
            "http://example.local/hook", "{\"a\":1}", "POST", 5, 3, 3,
            Guid.NewGuid().ToString("N"));

        using (var publisher = new DeadLetterPublisher(_infra.Kafka.GetBootstrapAddress(), "jobs.dead"))
        {
            await publisher.PublishAsync(envelope, 3, CancellationToken.None);
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _infra.Kafka.GetBootstrapAddress(),
            GroupId = "dlq-test-" + Guid.NewGuid(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("jobs.dead");
        try
        {
            var cr = consumer.Consume(TimeSpan.FromSeconds(15));
            Assert.NotNull(cr);
            Assert.Equal(envelope.ExecutionId.ToString(), cr.Message.Key);
            var attempt = cr.Message.Headers.First(h => h.Key == "x-attempt");
            Assert.Equal("3", System.Text.Encoding.UTF8.GetString(attempt.GetValueBytes()));
            var roundtrip = JsonSerializer.Deserialize<DispatchEnvelope>(cr.Message.Value);
            Assert.Equal(envelope.ExecutionId, roundtrip!.ExecutionId);
        }
        finally
        {
            consumer.Close();
        }
    }
}
