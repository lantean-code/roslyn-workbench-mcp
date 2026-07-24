namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record StateSequenceExecution
{
    public required IReadOnlyList<StateSequenceStepMeasurement> Steps { get; init; }

    public ExternalCommandMeasurement? ExternalCommand { get; init; }

    public WatcherStressMeasurement? WatcherStress { get; init; }
}
