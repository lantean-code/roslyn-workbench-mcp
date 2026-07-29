namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceTransactionRevision
{
    public required WorkspaceSnapshotId SnapshotId { get; init; }

    public required Solution Solution { get; init; }

    public required ChangeSummary Changes { get; init; }

    public required string Operation { get; init; }

    public required string Summary { get; init; }

    public required MutationPreview Preview { get; init; }
}
