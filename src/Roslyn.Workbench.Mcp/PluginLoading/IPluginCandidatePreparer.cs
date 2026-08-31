using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Converts bundled assemblies and discovered external packages into catalogue candidates.
/// </summary>
internal interface IPluginCandidatePreparer
{
    /// <summary>
    /// Composes the trusted plugin assemblies bundled with the host.
    /// </summary>
    /// <param name="bundledAssemblies">The bundled assemblies to include in discovery.</param>
    /// <returns>Prepared bundled plugins and any status entries produced while composing them.</returns>
    PluginCandidatePreparation PrepareBundled(IReadOnlyList<Assembly> bundledAssemblies);

    /// <summary>
    /// Loads and composes accepted external package candidates while rejecting duplicate plugin identifiers.
    /// </summary>
    /// <param name="discoveryResults">The plugin discovery results to include in the catalogue.</param>
    /// <param name="duplicatePluginIds">The external plugin identifiers rejected as duplicates.</param>
    /// <returns>Prepared external plugins, their load contexts and status entries for all candidates.</returns>
    PluginCandidatePreparation PrepareExternal(
        IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults,
        IReadOnlySet<string> duplicatePluginIds);
}
