namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is opened.
/// </summary>
internal sealed record WorkspaceOpenData
{
    /// <summary>
    /// The loaded workspace identity.
    /// </summary>
    [Description("The loaded workspace identity.")]
    public WorkspaceIdentity? Workspace { get; init; }

    /// <summary>
    /// The loaded project count.
    /// </summary>
    [Description("The loaded project count.")]
    public int ProjectCount { get; init; }

    /// <summary>
    /// The loaded document count.
    /// </summary>
    [Description("The loaded document count.")]
    public int DocumentCount { get; init; }

    /// <summary>
    /// Workspace load and advisory status diagnostics.
    /// </summary>
    [Description("Workspace load and advisory status diagnostics.")]
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
