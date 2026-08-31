using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Builds the immutable plugin catalogue published during host startup.
/// </summary>
internal interface IPluginCatalogLoader
{
    /// <summary>
    /// Discovers, validates and materializes bundled and external plugins.
    /// </summary>
    /// <param name="startupOptions">The configured external plugin directories.</param>
    /// <param name="bundledAssemblies">The bundled assemblies to include in discovery.</param>
    /// <param name="reservedToolNames">The host-owned tool names that external plugins may not publish.</param>
    /// <returns>The complete catalogue snapshot, including accepted tools and rejected-plugin status.</returns>
    PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null);
}
