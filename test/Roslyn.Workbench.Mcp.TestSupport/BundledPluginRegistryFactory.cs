using Roslyn.Workbench.Mcp.CodeActions;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class BundledPluginRegistryFactory
{
    public static PluginRegistry CreateRegistry()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);
        EnsureCodeActionToolsRegistered(registry);

        return registry;
    }

    public static void EnsureCodeActionToolsRegistered(PluginRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (registry.RegisteredTools.Any(static tool => string.Equals(tool.Metadata.Name, "list-code-actions", StringComparison.Ordinal)))
        {
            return;
        }

        new BundledCodeActionsPlugin().Register(registry);
    }
}
