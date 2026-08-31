namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Applies host file-system identity and physical containment rules to plugin package paths.
/// </summary>
internal sealed class PluginPackagePathPolicy : IPluginPackagePathPolicy
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IPhysicalPathContainment _pathContainment;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginPackagePathPolicy"/> class.
    /// </summary>
    /// <param name="pathComparison">The platform-aware rules used to canonicalize and compare paths.</param>
    /// <param name="pathContainment">The service that resolves links and verifies physical containment.</param>
    public PluginPackagePathPolicy(
        IWorkspacePathComparison pathComparison,
        IPhysicalPathContainment pathContainment)
    {
        _pathComparison = pathComparison;
        _pathContainment = pathContainment;
    }

    /// <summary>
    /// Creates a canonical path key using the host file system's comparison rules.
    /// </summary>
    /// <param name="path">The plugin path to normalize for comparison.</param>
    /// <returns>A canonical key suitable for duplicate-path detection.</returns>
    public FileSystemPathKey CreateKey(string path)
    {
        return _pathComparison.CreateKey(path);
    }

    /// <summary>
    /// Resolves a candidate path and verifies that it remains inside the package directory.
    /// </summary>
    /// <param name="packageDirectory">The directory containing the plugin package.</param>
    /// <param name="candidatePath">The candidate path to validate or normalize.</param>
    /// <param name="containedPath">The canonical path returned when containment succeeds.</param>
    /// <returns><see langword="true"/> when the canonical candidate is contained by the package; otherwise, <see langword="false"/>.</returns>
    public bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath)
    {
        return _pathContainment.TryGetContainedPath(
            packageDirectory,
            candidatePath,
            out containedPath);
    }
}
