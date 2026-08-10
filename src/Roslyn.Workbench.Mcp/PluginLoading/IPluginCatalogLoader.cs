using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginCatalogLoader
{
    PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null);
}
