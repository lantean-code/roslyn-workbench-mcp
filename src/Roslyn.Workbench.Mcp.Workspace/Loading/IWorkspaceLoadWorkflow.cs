namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Coordinates workspace loading, compatibility filtering, and workspace-root validation.
/// </summary>
internal interface IWorkspaceLoadWorkflow
{
    /// <summary>
    /// Loads and validates a workspace for use by the host.
    /// </summary>
    /// <param name="loadedPath">The canonical solution or project path to load.</param>
    /// <param name="workspaceRoot">The canonical root that must contain every retained project.</param>
    /// <param name="msBuildProperties">The optional MSBuild properties used during evaluation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The validated workspace state or a classified failure.</returns>
    ValueTask<ValidatedWorkspaceLoadResult> LoadAsync(
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken);
}
