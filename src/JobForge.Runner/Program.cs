using JobForge.Runner.Configuration;
using JobForge.Runner.Dedup;
using JobForge.Runner.Health;
using JobForge.Runner.Webhook;
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

builder.Services.AddHttpClient("webhook");
builder.Services.AddSingleton<IWebhookExecutor>(sp =>
    new WebhookExecutor(sp.GetRequiredService<IHttpClientFactory>().CreateClient("webhook")));
builder.Services.AddSingleton<IDedup>(sp => new RedisDedup(
    sp.GetRequiredService<IConnectionMultiplexer>(),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RunnerOptions>>().Value.DedupTtlSeconds));

var app = builder.Build();
app.MapGet("/", () => "jobforge-runner");
app.MapHealthChecks("/health/live",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.Run();
