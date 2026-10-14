using System.Diagnostics.Metrics;
using Confluent.Kafka;
using JobForge.Runner.Domain;
using JobForge.Runner.Reporting;

namespace JobForge.Runner.Processing;

public sealed class KafkaConsumerService : BackgroundService
{
    public const string MeterName = "JobForge.Runner";

    private readonly string _bootstrapServers;
    private readonly string _groupId;
    private readonly string _dispatchTopic;
    private readonly MessageProcessor _processor;
    private readonly TimeProvider _clock;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _attempts;
    private readonly Counter<long> _dlq;

    public KafkaConsumerService(
        string bootstrapServers,
        string groupId,
        string dispatchTopic,
        MessageProcessor processor,
        IStatusReporter reporter,
        TimeProvider clock,
        ILogger<KafkaConsumerService> logger,
        IMeterFactory? meterFactory = null)
    {
        _bootstrapServers = bootstrapServers;
        _groupId = groupId;
        _dispatchTopic = dispatchTopic;
        _processor = processor;
        _clock = clock;
        _logger = logger;
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);
        _attempts = _meter.CreateCounter<long>("jobforge_runner_attempts_total");
        _dlq = _meter.CreateCounter<long>("jobforge_dlq_published_total");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false
        };
        using var consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_, err) => _logger.LogError("Kafka error: {Reason}", err.Reason))
            .Build();
        consumer.Subscribe(_dispatchTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cr = consumer.Consume(TimeSpan.FromSeconds(1));
                if (cr is null) continue;

                ProcessResult outcome;
                try
                {
                    var envelope = DispatchEnvelope.TryParse(cr.Message.Value);
                    if (envelope is null)
                    {
                        _logger.LogWarning("Unparseable message at {TopicPartitionOffset}; committing", cr.TopicPartitionOffset);
                        outcome = ProcessResult.Skipped;
                    }
                    else
                    {
                        outcome = await _processor.ProcessAsync(envelope, stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing failed for {TopicPartitionOffset}; message will be redelivered", cr.TopicPartitionOffset);
                    continue;
                }

                _attempts.Add(1, new KeyValuePair<string, object?>("result", outcome.ToString().ToLowerInvariant()));
                if (outcome == ProcessResult.Dead) _dlq.Add(1);
                consumer.Commit(cr);
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
