namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Defines the entry point contract for one Roslyn Workbench plugin assembly.
/// </summary>
public interface IRoslynPlugin
{
    /// <summary>
    /// Gets the stable metadata describing the plugin.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Registers the plugin's query and mutation tools.
    /// </summary>
    /// <param name="registry">The registry that records tool descriptors and handlers.</param>
    void Register(IPluginRegistry registry);
}
