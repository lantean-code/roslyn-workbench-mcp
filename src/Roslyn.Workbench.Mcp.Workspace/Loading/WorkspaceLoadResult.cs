namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Captures the raw outcome of opening a Roslyn workspace before host validation.
/// </summary>
internal sealed class WorkspaceLoadResult
{
    /// <summary>
    /// Gets the owned loaded workspace when opening succeeded.
    /// </summary>
    public ILoadedWorkspace? Workspace { get; init; }

    /// <summary>
    /// Gets the solution produced by Roslyn when opening succeeded.
    /// </summary>
    public Solution? Solution { get; init; }

    /// <summary>
    /// Gets the project target-framework identity map when opening succeeded.
    /// </summary>
    public WorkspaceProjectTargetFrameworkMap? ProjectTargetFrameworks { get; init; }

    /// <summary>
    /// Gets diagnostics emitted while opening the workspace.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
