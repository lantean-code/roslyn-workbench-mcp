namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed record WorkspaceInstanceStatusUpdate
{
    public required string WorkspaceId { get; init; }

    public required WorkspaceLifecycleState State { get; init; }

    public long? TransactionRevision { get; init; }

    public string? CommitId { get; init; }

    public string? CommitPhase { get; init; }

    public TaskCompletionSource? Completion { get; init; }
}
