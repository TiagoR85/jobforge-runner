using System.Text;
using Confluent.Kafka;
using JobForge.Runner.Domain;

namespace JobForge.Runner.Processing;

public interface IDeadLetterPublisher
{
    Task PublishAsync(DispatchEnvelope envelope, int finalAttempt, CancellationToken ct);
}

public sealed class DeadLetterPublisher : IDeadLetterPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public DeadLetterPublisher(string bootstrapServers, string topic)
    {
        _topic = topic;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            ClientId = "jobforge-runner",
            EnableIdempotence = true
        }).Build();
    }

    public async Task PublishAsync(DispatchEnvelope envelope, int finalAttempt, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = envelope.ExecutionId.ToString(),
            Value = System.Text.Json.JsonSerializer.Serialize(envelope),
            Headers = new Headers { { "x-attempt", Encoding.UTF8.GetBytes(finalAttempt.ToString()) } }
        };
        await _producer.ProduceAsync(_topic, message, ct);
    }

    public void Dispose() => _producer.Dispose();
}
