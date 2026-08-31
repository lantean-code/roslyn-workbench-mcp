namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Discovers external plugin package candidates beneath configured search roots.
/// </summary>
internal interface IPluginPackageDiscovery
{
    /// <summary>
    /// Inspects each immediate child directory as an independent plugin package candidate.
    /// </summary>
    /// <param name="searchRoots">The directories whose immediate children may contain plugin packages.</param>
    /// <returns>The discovery result for each valid or rejected package candidate.</returns>
    IReadOnlyList<PluginPackageDiscoveryResult> Discover(IReadOnlyList<string> searchRoots);
}
