namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

internal sealed record WorkspaceInstanceStatus
{
    public int Version { get; init; } = 2;

    public required string InstanceId { get; init; }

    public required string LoadedPath { get; init; }

    public required string WorkspaceRoot { get; init; }

    public required WorkspaceLifecycleState WorkspaceState { get; init; }

    public long? TransactionRevision { get; init; }

    public string? CommitId { get; init; }

    public string? CommitPhase { get; init; }
}
