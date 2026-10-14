using System.Net;
using System.Text.Json;
using Confluent.Kafka;
using JobForge.Runner.Configuration;
using JobForge.Runner.Dedup;
using JobForge.Runner.Domain;
using JobForge.Runner.Processing;
using JobForge.Runner.Tests.Fixtures;
using JobForge.Runner.Webhook;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace JobForge.Runner.Tests;

[Collection("containers")]
public class KafkaConsumerIntegrationTests
{
    private readonly ContainersFixture _infra;
    public KafkaConsumerIntegrationTests(ContainersFixture infra) => _infra = infra;

    [Fact]
    public async Task Consumed_message_executes_webhook_reports_and_commits()
    {
        using var webhook = new HttpListener();
        var port = FreePort();
        webhook.Prefixes.Add($"http://127.0.0.1:{port}/hook/");
        webhook.Start();
        var webhookTask = Task.Run(async () =>
        {
            var ctx = await webhook.GetContextAsync();
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        });

        var envelope = new DispatchEnvelope(Guid.NewGuid(), Guid.NewGuid(),
            $"http://127.0.0.1:{port}/hook/", "{}", "POST", 5, 3, 0,
            Guid.NewGuid().ToString("N"));

        await ProduceAsync(_infra.Kafka.GetBootstrapAddress(), "jobs.dispatch",
            envelope.ExecutionId.ToString(), JsonSerializer.Serialize(envelope));

        await using var redis = ConnectionMultiplexer.Connect(_infra.Redis.GetConnectionString());
        var dedup = new RedisDedup(redis, ttlSeconds: 60);
        var reporter = new FakeStatusReporter();
        var processor = new MessageProcessor(dedup, new WebhookExecutor(new HttpClient()),
            reporter, new RecordingDlq(), TimeProvider.System,
            Options.Create(new RunnerOptions()), NullLogger<MessageProcessor>.Instance);

        var ct = new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token;
        var service = new KafkaConsumerService(
            _infra.Kafka.GetBootstrapAddress(), "jobs.runners-test-" + Guid.NewGuid(),
            "jobs.dispatch", processor, reporter, TimeProvider.System,
            NullLogger<KafkaConsumerService>.Instance);
        await service.StartAsync(ct);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline &&
                   !reporter.Reports.Any(r => r.Status == "succeeded" &&
                       r.ExecutionId == envelope.ExecutionId.ToString()))
                await Task.Delay(200, ct);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            webhook.Stop();
        }

        Assert.Contains(reporter.Reports, r => r.Status == "running");
        Assert.Contains(reporter.Reports, r => r.Status == "succeeded");
        await webhookTask;
    }

    private static async Task ProduceAsync(string bootstrap, string topic, string key, string value)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrap, AllowAutoCreateTopics = true };
        using var producer = new ProducerBuilder<string, string>(config).Build();
        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value });
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private sealed class RecordingDlq : IDeadLetterPublisher
    {
        public Task PublishAsync(DispatchEnvelope envelope, int finalAttempt, CancellationToken ct)
            => Task.CompletedTask;
    }
}
