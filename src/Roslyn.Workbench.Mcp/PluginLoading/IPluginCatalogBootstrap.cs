using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginCatalogBootstrap
{
    PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null);
}
