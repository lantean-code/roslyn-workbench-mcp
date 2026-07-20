namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record PhaseTraceObservation
{
    public required string Operation { get; init; }

    public required string Phase { get; init; }

    public required double ElapsedMilliseconds { get; init; }
}
