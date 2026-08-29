namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is opened.
/// </summary>
internal sealed record WorkspaceOpenData
{
    /// <summary>
    /// Gets the loaded workspace identity.
    /// </summary>
    [Description("The loaded workspace identity.")]
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
    /// Gets workspace load and advisory status diagnostics.
    /// </summary>
    [Description("Workspace load and advisory status diagnostics.")]
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
