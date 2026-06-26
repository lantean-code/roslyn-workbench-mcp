using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the lifecycle state of the loaded workspace.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceLifecycleState>))]
public enum WorkspaceLifecycleState
{
    /// <summary>
    /// No workspace is loaded.
    /// </summary>
    Unloaded,

    /// <summary>
    /// A workspace is loaded and ready.
    /// </summary>
    Ready,

    /// <summary>
    /// A transaction is active.
    /// </summary>
    TransactionActive,

    /// <summary>
    /// The active transaction is conflicted.
    /// </summary>
    TransactionConflicted,

    /// <summary>
    /// The workspace is out of date and requires reload.
    /// </summary>
    WorkspaceOutOfDate,
}
