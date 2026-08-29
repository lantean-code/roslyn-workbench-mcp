using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned by workspace status.
/// </summary>
internal sealed record WorkspaceStatusData
{
    /// <summary>
    /// Gets the workspace lifecycle state.
    /// </summary>
    [Description("The workspace lifecycle state.")]
    public WorkspaceLifecycleState State { get; init; }

    /// <summary>
    /// Gets the loaded workspace identity, when present.
    /// </summary>
    [Description("The loaded workspace identity, when present.")]
    public WorkspaceIdentity? Workspace { get; init; }

    /// <summary>
    /// Gets the loaded project count, when present.
    /// </summary>
    [Description("The loaded project count, when present.")]
    public int? ProjectCount { get; init; }

    /// <summary>
    /// Gets the loaded document count, when present.
    /// </summary>
    [Description("The loaded document count, when present.")]
    public int? DocumentCount { get; init; }

    /// <summary>
    /// Gets project load and advisory workspace-status diagnostics.
    /// </summary>
    [Description("Project load and advisory workspace-status diagnostics.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DiagnosticInfo>? LoadDiagnostics { get; init; }

    /// <summary>
    /// Gets the active transaction info, when present.
    /// </summary>
    [Description("The active transaction info, when present.")]
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workspace requires reload.
    /// </summary>
    [Description("Whether the workspace requires reload.")]
    public bool ReloadRequired { get; init; }

    /// <summary>
    /// Gets the first detected external Workspace input change, when present.
    /// </summary>
    [Description("The first detected external Workspace input change, when present.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkspaceExternalChangeData? ExternalChange { get; init; }

    /// <summary>
    /// Gets other live Roslyn Workbench MCP instances using this workspace.
    /// </summary>
    [Description("Other live Roslyn Workbench MCP instances using this workspace.")]
    public IReadOnlyList<WorkspaceInstanceInfo> Instances { get; init; } = [];

    /// <summary>
    /// Gets the process-local error-reporting approval applying to this Workspace and epoch.
    /// </summary>
    [Description("The process-local error-reporting approval applying to this Workspace and epoch.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorReportingConsent { get; init; }
}
