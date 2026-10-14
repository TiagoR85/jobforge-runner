using JobForge.Orchestrator.Grpc;

namespace JobForge.Runner.Reporting;

public interface IStatusReporter
{
    Task ReportAsync(StatusReport report, CancellationToken ct);
}
