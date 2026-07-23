namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record PhaseTraceSummary
{
    public required string Operation { get; init; }

    public required string Phase { get; init; }

    public required int Count { get; init; }

    public required double MedianMilliseconds { get; init; }

    public required double P95Milliseconds { get; init; }

    public required double TotalMilliseconds { get; init; }

    public required double MedianToolSharePercent { get; init; }
}
