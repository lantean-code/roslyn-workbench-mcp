namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the structured payload returned when a workspace is opened.
/// </summary>
internal sealed record WorkspaceOpenData
{
    /// <summary>
    /// Gets the loaded workspace identity.
    /// </summary>
    public WorkspaceIdentity? Workspace { get; init; }

    /// <summary>
    /// Gets the loaded project count.
    /// </summary>
    public int ProjectCount { get; init; }

    /// <summary>
    /// Gets the loaded document count.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// Gets workspace load and advisory status diagnostics.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
