using System.Composition.Hosting;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class MefPluginComposer : IPluginComposer
{
    public PluginCompositionResult Configure(Assembly assembly, IPluginConfiguration configuration)
    {
        using var container = new ContainerConfiguration()
            .WithAssembly(assembly)
            .CreateContainer();
        var plugins = container.GetExports<IRoslynPlugin>().ToArray();
        if (plugins.Length != 1)
        {
            return PluginCompositionResult.Failure(
                plugins.Length == 0
                    ? "Plugin assembly does not compose an IRoslynPlugin export."
                    : "Plugin assembly composes multiple IRoslynPlugin exports.");
        }

        plugins[0].Configure(configuration);
        return PluginCompositionResult.Success();
    }
}
