namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Captures all immutable state required to execute against one loaded Workspace at a specific snapshot.
/// </summary>
internal sealed record WorkspaceSessionSnapshot
{
    /// <summary>
    /// Gets the identifier of the last committed solution snapshot.
    /// </summary>
    public required WorkspaceSnapshotId CommittedSnapshotId { get; init; }

    /// <summary>
    /// Gets the current lifecycle state.
    /// </summary>
    public required WorkspaceLifecycleState State { get; init; }

    /// <summary>
    /// Gets the stable Workspace identity and current load epoch.
    /// </summary>
    public required WorkspaceIdentity Workspace { get; init; }

    /// <summary>
    /// Gets the owned Roslyn Workspace and its immutable baseline solution.
    /// </summary>
    public required ILoadedWorkspace LoadedWorkspace { get; init; }

    /// <summary>
    /// Gets the solution visible to the current committed or transactional snapshot.
    /// </summary>
    public required Solution CurrentSolution { get; init; }

    /// <summary>
    /// Gets target-framework identities for projects in the loaded Workspace.
    /// </summary>
    public WorkspaceProjectTargetFrameworkMap ProjectTargetFrameworks { get; init; } = WorkspaceProjectTargetFrameworkMap.Empty;

    /// <summary>
    /// Gets the MSBuild global properties used to evaluate the Workspace.
    /// </summary>
    public WorkspaceMsBuildProperties? MsBuildProperties { get; init; }

    /// <summary>
    /// Gets the active transaction and its revision history.
    /// </summary>
    public WorkspaceTransaction? Transaction { get; init; }

    /// <summary>
    /// Gets the certified inputs whose stability underpins this loaded session.
    /// </summary>
    public required WorkspaceInputManifest InputManifest { get; init; }

    /// <summary>
    /// Gets the gate coordinating shared and exclusive operations on this session.
    /// </summary>
    public required IWorkspaceOperationGate OperationGate { get; init; }

    /// <summary>
    /// Gets the complete identity of the solution snapshot visible to callers.
    /// </summary>
    public required WorkspaceSnapshotIdentity CurrentSnapshotIdentity { get; init; }

    /// <summary>
    /// Gets the number of projects loaded into the session.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the number of documents loaded into the session.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets diagnostics retained from Workspace loading and input evaluation.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
