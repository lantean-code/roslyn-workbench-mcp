using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Composes a loaded assembly into one validated plugin ready for catalogue publication.
/// </summary>
internal interface ILoadedPluginPreparer
{
    /// <summary>
    /// Composes a loaded plugin and materializes its validated catalogue metadata.
    /// </summary>
    /// <param name="assembly">The loaded plugin entry assembly to compose.</param>
    /// <param name="entryPoint">The metadata read from the entry assembly before it was loaded.</param>
    /// <param name="contractAccessibility">The contract accessibility available to the plugin assembly.</param>
    /// <returns>The composed plugin, its registered tools and any preparation error.</returns>
    PreparedCatalogPlugin Prepare(
        Assembly assembly,
        PluginEntryPointMetadata entryPoint,
        PluginContractAccessibility contractAccessibility);
}
