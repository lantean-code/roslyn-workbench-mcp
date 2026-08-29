namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is reloaded.
/// </summary>
internal sealed record WorkspaceReloadData
{
    /// <summary>
    /// Gets the reloaded workspace identity.
    /// </summary>
    [Description("The reloaded workspace identity.")]
    public WorkspaceIdentity? Workspace { get; init; }

    /// <summary>
    /// Gets the loaded project count.
    /// </summary>
    [Description("The loaded project count.")]
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the loaded document count.
    /// </summary>
    [Description("The loaded document count.")]
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets the workspace load diagnostics.
    /// </summary>
    [Description("The workspace load diagnostics.")]
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
