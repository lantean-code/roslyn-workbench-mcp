namespace Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

internal sealed record HostShutdownResult
{
    public int? ExitCode { get; init; }

    public bool ForcedTermination { get; init; }

    public required string StandardError { get; init; }
}
