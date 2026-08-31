namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Evaluates a project to discover files, imports and item globs that can affect its loaded Workspace state.
/// </summary>
internal interface IWorkspaceProjectInputResolver
{
    /// <summary>
    /// Resolves the monitored inputs for a project under the supplied MSBuild global properties.
    /// </summary>
    /// <param name="projectPath">The project path, or <see langword="null"/> when the project is not file-backed.</param>
    /// <param name="msBuildProperties">The optional global properties used for evaluation.</param>
    /// <returns>The resolved inputs or a structured evaluation failure.</returns>
    WorkspaceProjectInputResolution Resolve(
        string? projectPath,
        WorkspaceMsBuildProperties? msBuildProperties = null);
}
