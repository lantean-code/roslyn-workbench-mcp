using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record StateSequenceMeasurement
{
    public required int Iteration { get; init; }

    public required IReadOnlyList<StateSequenceStepMeasurement> Steps { get; init; }

    public ExternalCommandMeasurement? ExternalCommand { get; init; }

    public WatcherStressMeasurement? WatcherStress { get; init; }

    public required double RestorationMilliseconds { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
