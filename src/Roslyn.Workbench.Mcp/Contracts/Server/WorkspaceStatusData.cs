using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the structured payload returned by workspace status.
/// </summary>
public sealed record WorkspaceStatusData
{
    /// <summary>
    /// Gets the workspace lifecycle state.
    /// </summary>
    public WorkspaceLifecycleState State { get; init; }

    /// <summary>
    /// Gets the loaded workspace identity, when present.
    /// </summary>
    public WorkspaceIdentity? Workspace { get; init; }

    /// <summary>
    /// Gets the loaded project count, when present.
    /// </summary>
    public int? ProjectCount { get; init; }

    /// <summary>
    /// Gets the loaded document count, when present.
    /// </summary>
    public int? DocumentCount { get; init; }

    /// <summary>
    /// Gets project load and advisory workspace-status diagnostics.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DiagnosticInfo>? LoadDiagnostics { get; init; }

    /// <summary>
    /// Gets the active transaction info, when present.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workspace requires reload.
    /// </summary>
    public bool ReloadRequired { get; init; }

    /// <summary>
    /// Gets other live Roslyn Workbench MCP instances using this workspace.
    /// </summary>
    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; init; } = [];
}
