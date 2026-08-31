namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Normalises workspace inputs, inspects project compatibility, and opens Roslyn workspaces.
/// </summary>
internal interface IWorkspaceLoader
{
    /// <summary>
    /// Validates and canonicalises a solution or C# project path.
    /// </summary>
    /// <param name="path">The path supplied by the caller.</param>
    /// <returns>The canonical supported path, or <see langword="null"/> when the path is invalid or unsupported.</returns>
    string? NormalizeOpenPath(string path);

    /// <summary>
    /// Trims a caller-friendly workspace alias.
    /// </summary>
    /// <param name="alias">The optional alias supplied by the caller.</param>
    /// <returns>The trimmed alias, or <see langword="null"/> when no meaningful alias was supplied.</returns>
    string? NormalizeAlias(string? alias);

    /// <summary>
    /// Determines whether a project can be loaded as an SDK-style project.
    /// </summary>
    /// <param name="projectPath">The project file to inspect.</param>
    /// <param name="msBuildProperties">The optional MSBuild properties used during evaluation.</param>
    /// <returns>The compatibility result and any diagnostics produced while evaluating the project.</returns>
    (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(
        string projectPath,
        WorkspaceMsBuildProperties? msBuildProperties);

    /// <summary>
    /// Opens a solution or C# project and captures its load diagnostics and target-framework identities.
    /// </summary>
    /// <param name="path">The canonical solution or project path.</param>
    /// <param name="msBuildProperties">The optional MSBuild properties used during evaluation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace load result.</returns>
    ValueTask<WorkspaceLoadResult> LoadAsync(
        string path,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken);
}
