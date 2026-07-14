namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Defines the entry point contract for one Roslyn Workbench plugin assembly.
/// </summary>
public interface IRoslynPlugin
{
    /// <summary>
    /// Configures the tools supplied by the plugin.
    /// </summary>
    /// <param name="configuration">The startup-only plugin configuration.</param>
    void Configure(IPluginConfiguration configuration);
}
