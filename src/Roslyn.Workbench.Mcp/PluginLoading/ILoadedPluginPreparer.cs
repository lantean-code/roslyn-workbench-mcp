using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface ILoadedPluginPreparer
{
    PreparedCatalogPlugin Prepare(
        Assembly assembly,
        PluginEntryPointMetadata entryPoint,
        PluginContractAccessibility contractAccessibility);
}
