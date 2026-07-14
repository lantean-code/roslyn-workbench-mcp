using System.Reflection;

namespace Roslyn.Workbench.Mcp;

internal interface ILoadedPluginPreparer
{
    PreparedCatalogPlugin Prepare(Assembly assembly, PluginEntryPointMetadata entryPoint);
}
