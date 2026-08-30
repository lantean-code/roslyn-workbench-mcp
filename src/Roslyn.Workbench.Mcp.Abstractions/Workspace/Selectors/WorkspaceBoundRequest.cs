namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

/// <summary>
/// Provides the optional workspace selector shared by workspace-executed requests.
/// </summary>
public abstract record WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the target workspace selector.
    /// </summary>
    public WorkspaceSelector? Workspace { get; init; }
}
