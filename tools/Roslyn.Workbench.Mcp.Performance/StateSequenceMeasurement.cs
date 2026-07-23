namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record StateSequenceMeasurement
{
    public required int Iteration { get; init; }

    public required IReadOnlyList<StateSequenceStepMeasurement> Steps { get; init; }

    public required double RestorationMilliseconds { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
