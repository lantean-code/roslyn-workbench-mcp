namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Certifies that a manifest represents a stable view of inputs throughout Workspace loading.
/// </summary>
internal interface IWorkspaceInputCertification : IDisposable
{
    /// <summary>
    /// Completes certification and records any change observed while the manifest was built.
    /// </summary>
    /// <param name="manifest">The manifest assembled from the loaded Workspace.</param>
    /// <returns>The certified manifest, including a load-time change when one was observed.</returns>
    WorkspaceInputManifest Complete(WorkspaceInputManifest manifest);

    /// <summary>
    /// Completes certification while excluding paths whose load-time changes are intentionally irrelevant.
    /// </summary>
    /// <param name="manifest">The manifest assembled from the loaded Workspace.</param>
    /// <param name="ignoredPaths">Paths to omit when evaluating the observed change.</param>
    /// <returns>The certified manifest, including any non-ignored load-time change.</returns>
    WorkspaceInputManifest Complete(
        WorkspaceInputManifest manifest,
        IEnumerable<string> ignoredPaths);
}
