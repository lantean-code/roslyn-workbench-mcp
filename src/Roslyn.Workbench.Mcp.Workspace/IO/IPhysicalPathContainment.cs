namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Verifies physical path containment while following existing symbolic links.
/// </summary>
internal interface IPhysicalPathContainment
{
    /// <summary>
    /// Attempts to certify that a candidate is the root itself or is contained beneath it.
    /// </summary>
    /// <param name="rootDirectory">The containment root.</param>
    /// <param name="candidatePath">The path to test against the containment root.</param>
    /// <param name="containedPath">The canonical candidate when containment succeeds.</param>
    /// <returns><see langword="true"/> when containment is certified; otherwise, <see langword="false"/>.</returns>
    bool TryGetContainedPath(string rootDirectory, string candidatePath, out string containedPath);

    /// <summary>
    /// Attempts to certify that a candidate is strictly beneath the root.
    /// </summary>
    /// <param name="rootDirectory">The containment root.</param>
    /// <param name="candidatePath">The path to test for strict containment beneath the root.</param>
    /// <param name="containedPath">The canonical candidate when containment succeeds.</param>
    /// <returns><see langword="true"/> when strict containment is certified; otherwise, <see langword="false"/>.</returns>
    bool TryGetStrictlyContainedPath(string rootDirectory, string candidatePath, out string containedPath);
}
