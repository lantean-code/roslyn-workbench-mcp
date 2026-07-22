namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record CancellationMeasurement
{
    public required int Iteration { get; init; }

    public required double CancellationRequestedAfterMilliseconds { get; init; }

    public required double ClientCancellationLatencyMilliseconds { get; init; }

    public required double ExclusiveLeaseRecoveryMilliseconds { get; init; }

    public required bool CompletedBeforeCancellation { get; init; }

    public required bool OperationCanceled { get; init; }
}
