namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed record CacheMetricObservation
{
    public required string Family { get; init; }

    public required string Metric { get; init; }

    public required long Value { get; init; }
}
