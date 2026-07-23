namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record StateSequenceExecution
{
    public required IReadOnlyList<StateSequenceStepMeasurement> Steps { get; init; }
}
