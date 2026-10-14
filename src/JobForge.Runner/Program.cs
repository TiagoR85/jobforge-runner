using JobForge.Runner.Configuration;
using JobForge.Runner.Health;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

RequiredConfig.Ensure(builder.Configuration,
    "ConnectionStrings:Redis", "Kafka:BootstrapServers", "Orchestrator:GrpcUrl");

builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection("Runner"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["ConnectionStrings:Redis"]!));

builder.Services.AddHealthChecks()
    .AddCheck("self", static () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();
app.MapGet("/", () => "jobforge-runner");
app.MapHealthChecks("/health/live",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.Run();
