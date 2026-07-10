using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents the structured payload returned when a workspace is reloaded.
/// </summary>
public sealed record WorkspaceReloadData
{
    /// <summary>
    /// Gets the reloaded workspace identity.
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
