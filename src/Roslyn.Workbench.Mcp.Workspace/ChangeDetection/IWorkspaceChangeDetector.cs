namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Captures the files and directories that define a loaded Workspace and detects changes that require reload.
/// </summary>
internal interface IWorkspaceChangeDetector
{
    /// <summary>
    /// Begins monitoring inputs while a Workspace is being loaded so changes during manifest construction are detected.
    /// </summary>
    /// <param name="workspaceRoot">The trusted root that bounds writable Workspace inputs.</param>
    /// <returns>A certification session that completes the initial input manifest.</returns>
    IWorkspaceInputCertification BeginCertification(string workspaceRoot);

    /// <summary>
    /// Builds a manifest of evaluated projects, loaded documents and monitored external inputs.
    /// </summary>
    /// <param name="solution">The loaded Roslyn solution.</param>
    /// <param name="loadedPath">The solution or project path used to load the Workspace.</param>
    /// <param name="workspaceRoot">The trusted Workspace root.</param>
    /// <param name="certification">The active load-time certification session.</param>
    /// <param name="msBuildProperties">The optional global properties used for project evaluation.</param>
    /// <param name="cancellationToken">Cancels manifest construction.</param>
    /// <returns>The manifest describing the inputs whose changes make the Workspace stale.</returns>
    WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputCertification certification,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether any tracked input differs from the supplied manifest.
    /// </summary>
    /// <param name="manifest">The previously certified input manifest.</param>
    /// <param name="cancellationToken">Cancels the comparison.</param>
    /// <returns><see langword="true"/> when an input changed or can no longer be certified; otherwise, <see langword="false"/>.</returns>
    bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken);
}
