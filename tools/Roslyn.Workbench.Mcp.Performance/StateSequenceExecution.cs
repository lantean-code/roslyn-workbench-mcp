namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record StateSequenceExecution
{
    public required IReadOnlyList<StateSequenceStepMeasurement> Steps { get; init; }
}
