using System.Diagnostics.Metrics;
using JobForge.Runner.Configuration;
using JobForge.Runner.Dedup;
using JobForge.Runner.Health;
using JobForge.Runner.Processing;
using JobForge.Runner.Reporting;
using JobForge.Runner.Webhook;
using NLog.Web;
using OpenTelemetry.Metrics;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

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

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDeadLetterPublisher>(sp => new DeadLetterPublisher(
    builder.Configuration["Kafka:BootstrapServers"]!,
    builder.Configuration["Kafka:DeadTopic"] ?? "jobs.dead"));
builder.Services.AddSingleton<MessageProcessor>();
builder.Services.AddHostedService(sp => new KafkaConsumerService(
    builder.Configuration["Kafka:BootstrapServers"]!,
    builder.Configuration["Kafka:GroupId"] ?? "jobs.runners",
    builder.Configuration["Kafka:DispatchTopic"] ?? "jobs.dispatch",
    sp.GetRequiredService<MessageProcessor>(),
    sp.GetRequiredService<IStatusReporter>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<ILogger<KafkaConsumerService>>(),
    sp.GetRequiredService<IMeterFactory>()));
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(KafkaConsumerService.MeterName)
        .AddPrometheusExporter());
builder.Services.AddSingleton(_ => Grpc.Net.Client.GrpcChannel.ForAddress(
    builder.Configuration["Orchestrator:GrpcUrl"]!,
    new Grpc.Net.Client.GrpcChannelOptions { DisposeHttpClient = true }));
builder.Services.AddSingleton<GrpcStatusReporter>();
builder.Services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<GrpcStatusReporter>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<GrpcStatusReporter>());

var app = builder.Build();
app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
app.MapGet("/", () => "jobforge-runner");
app.MapHealthChecks("/health/live",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
app.Run();
