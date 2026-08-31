namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Applies platform-aware identity and package-containment rules to plugin paths.
/// </summary>
internal interface IPluginPackagePathPolicy
{
    /// <summary>
    /// Creates a canonical path key using the host file system's comparison rules.
    /// </summary>
    /// <param name="path">The plugin path to normalize for comparison.</param>
    /// <returns>A canonical key suitable for duplicate-path detection.</returns>
    FileSystemPathKey CreateKey(string path);

    /// <summary>
    /// Resolves a candidate path and verifies that it remains inside the package directory.
    /// </summary>
    /// <param name="packageDirectory">The directory containing the plugin package.</param>
    /// <param name="candidatePath">The candidate path to validate or normalize.</param>
    /// <param name="containedPath">The canonical path returned when containment succeeds.</param>
    /// <returns><see langword="true"/> when the canonical candidate is contained by the package; otherwise, <see langword="false"/>.</returns>
    bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath);
}
