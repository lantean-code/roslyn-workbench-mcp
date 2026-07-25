namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Describes another live Roslyn Workbench MCP instance using a workspace.
/// </summary>
public sealed record WorkspaceInstanceInfo
{
    /// <summary>Gets the instance identifier.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Gets the canonical loaded solution or project path.</summary>
    public string LoadedPath { get; init; } = string.Empty;

    /// <summary>Gets the canonical repository or workspace root.</summary>
    public string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>Gets the workspace lifecycle state reported by the instance.</summary>
    public WorkspaceLifecycleState WorkspaceState { get; init; }

    /// <summary>Gets the staged transaction revision, when one is active.</summary>
    public long? TransactionRevision { get; init; }

    /// <summary>Gets the durable commit identifier, when a commit is active.</summary>
    public string? CommitId { get; init; }

    /// <summary>Gets the advisory commit phase, when a commit is active.</summary>
    public string? CommitPhase { get; init; }
}
