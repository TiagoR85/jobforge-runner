using JobForge.Runner.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace JobForge.Runner.Tests.Fixtures;

public sealed class RunnerFactory
{
    public required ContainersFixture Infra { get; init; }

    public IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = Infra.Redis.GetConnectionString(),
            ["Kafka:BootstrapServers"] = Infra.Kafka.GetBootstrapAddress(),
            ["Kafka:GroupId"] = "jobs.runners",
            ["Kafka:DispatchTopic"] = "jobs.dispatch",
            ["Kafka:DeadTopic"] = "jobs.dead",
            ["Orchestrator:GrpcUrl"] = "http://127.0.0.1:1",
            ["Runner:DedupTtlSeconds"] = "3600",
            ["Runner:MaxBackoffSeconds"] = "30",
        })
        .Build();

    public RunnerOptions Options => Configuration.GetSection("Runner").Get<RunnerOptions>() ?? new();
}
