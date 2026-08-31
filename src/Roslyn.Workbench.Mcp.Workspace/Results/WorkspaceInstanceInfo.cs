namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Describes another live Roslyn Workbench MCP instance using a workspace.
/// </summary>
public sealed record WorkspaceInstanceInfo
{
    /// <summary>
    /// Identifier of the other Roslyn Workbench server instance.
    /// </summary>
    [Description("Identifier of the other Roslyn Workbench server instance.")]
    public required string InstanceId { get; init; }

    /// <summary>
    /// Absolute solution or project path loaded by the other instance.
    /// </summary>
    [Description("Absolute solution or project path loaded by the other instance.")]
    public required string LoadedPath { get; init; }

    /// <summary>
    /// Absolute coordination and transaction root used by the other instance.
    /// </summary>
    [Description("Absolute coordination and transaction root used by the other instance.")]
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Workspace lifecycle state reported by the other instance.
    /// </summary>
    [Description("Workspace lifecycle state reported by the other instance.")]
    public WorkspaceLifecycleState WorkspaceState { get; init; }

    /// <summary>
    /// Staged transaction revision in the other instance, when a transaction is active.
    /// </summary>
    [Description("Staged transaction revision in the other instance, when a transaction is active.")]
    public long? TransactionRevision { get; init; }

    /// <summary>
    /// Durable commit identifier reported by the other instance, when a commit is active.
    /// </summary>
    [Description("Durable commit identifier reported by the other instance, when a commit is active.")]
    public string? CommitId { get; init; }

    /// <summary>
    /// Advisory durable commit phase reported by the other instance, when a commit is active.
    /// </summary>
    [Description("Advisory durable commit phase reported by the other instance, when a commit is active.")]
    public string? CommitPhase { get; init; }
}
