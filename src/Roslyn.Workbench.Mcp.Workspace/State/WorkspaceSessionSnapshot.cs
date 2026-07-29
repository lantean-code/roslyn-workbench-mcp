namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed record WorkspaceSessionSnapshot
{
    public required WorkspaceSnapshotId CommittedSnapshotId { get; init; }

    public required WorkspaceLifecycleState State { get; init; }

    public required WorkspaceIdentity Workspace { get; init; }

    public required ILoadedWorkspace LoadedWorkspace { get; init; }

    public required Solution CurrentSolution { get; init; }

    public WorkspaceTransaction? Transaction { get; init; }

    public required WorkspaceInputManifest InputManifest { get; init; }

    public required IWorkspaceOperationGate OperationGate { get; init; }

    public required WorkspaceSnapshotIdentity CurrentSnapshotIdentity { get; init; }

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
