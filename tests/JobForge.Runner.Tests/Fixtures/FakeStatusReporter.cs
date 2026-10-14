using JobForge.Orchestrator.Grpc;
using JobForge.Runner.Reporting;

namespace JobForge.Runner.Tests.Fixtures;

public sealed class FakeStatusReporter : IStatusReporter
{
    public List<StatusReport> Reports { get; } = [];
    public Func<StatusReport, bool> Ack { get; set; } = static _ => true;

    public Task ReportAsync(StatusReport report, CancellationToken ct)
    {
        Reports.Add(report);
        return Task.CompletedTask;
    }
}
