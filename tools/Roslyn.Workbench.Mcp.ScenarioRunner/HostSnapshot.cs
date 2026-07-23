namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal readonly record struct HostSnapshot
{
    public required TimeSpan CpuTime { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }
}
