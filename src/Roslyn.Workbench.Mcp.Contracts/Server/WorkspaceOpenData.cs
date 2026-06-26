using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when a workspace is opened.
/// </summary>
public sealed record WorkspaceOpenData
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
    /// Gets the workspace load diagnostics.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
