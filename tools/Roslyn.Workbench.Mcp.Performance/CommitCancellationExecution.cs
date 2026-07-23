namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record CommitCancellationExecution
{
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
}
