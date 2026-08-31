using System.Composition.Hosting;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Uses MEF to require exactly one plugin export and invoke its registration entry point.
/// </summary>
internal sealed class MefPluginComposer : IPluginComposer
{
    /// <summary>
    /// Composes exactly one plugin export and lets it populate the supplied configuration.
    /// </summary>
    /// <param name="assembly">The loaded plugin entry assembly to compose.</param>
    /// <param name="configuration">The registration target populated by the plugin export.</param>
    /// <returns>A successful result when exactly one plugin is configured; otherwise, a composition error.</returns>
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
