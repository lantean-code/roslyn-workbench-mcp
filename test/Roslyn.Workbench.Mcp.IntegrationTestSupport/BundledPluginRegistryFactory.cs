using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class BundledPluginRegistryFactory
{
    public static PluginRegistry CreateRegistry()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        return registry;
    }
}
