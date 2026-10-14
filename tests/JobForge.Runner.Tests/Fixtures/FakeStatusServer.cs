using Grpc.Core;
using JobForge.Orchestrator.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace JobForge.Runner.Tests.Fixtures;

public sealed class FakeStatusServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    public List<StatusReport> Received { get; } = [];
    public Func<StatusReport, bool> Persist { get; set; } = static _ => true;
    public volatile bool Down;

    private FakeStatusServer(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    public static async Task<FakeStatusServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o =>
            o.Listen(System.Net.IPAddress.Loopback, 0, lo => lo.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        var holder = new Holder();
        builder.Services.AddSingleton(holder);
        var app = builder.Build();
        var server = new FakeStatusServer(app);
        holder.Server = server;
        app.MapGrpcService<Impl>();
        await app.StartAsync();
        var port = app.Urls.First().Split(':').Last();
        server.Url = $"http://127.0.0.1:{port}";
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private sealed class Holder
    {
        public FakeStatusServer Server { get; set; } = null!;
    }

    private sealed class Impl(Holder holder) : ExecutionStatus.ExecutionStatusBase
    {
        public override async Task Report(
            IAsyncStreamReader<StatusReport> requestStream,
            IServerStreamWriter<ReportAck> responseStream,
            ServerCallContext context)
        {
            var owner = holder.Server;
            await foreach (var report in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (owner.Down) throw new RpcException(new Status(StatusCode.Unavailable, "down"));
                lock (owner.Received) owner.Received.Add(report);
                await responseStream.WriteAsync(new ReportAck
                {
                    ExecutionId = report.ExecutionId,
                    Persisted = owner.Persist(report)
                });
            }
        }
    }
}
