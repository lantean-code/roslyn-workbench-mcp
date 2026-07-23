namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record CommitCancellationMeasurement
{
    public required int Iteration { get; init; }

    public required CommitCancellationBoundary Boundary { get; init; }

    public required string ObservedPhase { get; init; }

    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double CancellationNotificationMilliseconds { get; init; }

    public required double CompletionAfterCancellationMilliseconds { get; init; }

    public required double SettlementMilliseconds { get; init; }

    public required bool OperationCanceled { get; init; }

    public required bool Committed { get; init; }

    public required int PreviewDocumentCount { get; init; }

    public required int? PostCancellationPreviewDocumentCount { get; init; }

    public required RecoveryEvidence RecoveryEvidence { get; init; }

    public required double RestorationMilliseconds { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
