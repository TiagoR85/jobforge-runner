namespace JobForge.Runner.Configuration;

public sealed class RunnerOptions
{
    public int DedupTtlSeconds { get; set; } = 3600;
    public int MaxBackoffSeconds { get; set; } = 30;
}
