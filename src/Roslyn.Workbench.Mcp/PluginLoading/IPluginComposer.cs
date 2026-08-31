using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Locates one plugin export in an assembly and lets it register its tools and services.
/// </summary>
internal interface IPluginComposer
{
    /// <summary>
    /// Composes exactly one plugin export and lets it populate the supplied configuration.
    /// </summary>
    /// <param name="assembly">The loaded plugin entry assembly to compose.</param>
    /// <param name="configuration">The registration target populated by the plugin export.</param>
    /// <returns>The composed plugin export or the reason composition failed.</returns>
    PluginCompositionResult Configure(Assembly assembly, IPluginConfiguration configuration);
}
