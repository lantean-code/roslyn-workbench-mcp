using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

/// <summary>
/// Registers the bundled first-party plugin assembly.
/// </summary>
public sealed class BundledCorePlugin : IRoslynPlugin
{
    /// <summary>
    /// Gets the bundled plugin metadata.
    /// </summary>
    public PluginMetadata Metadata => new()
    {
        PluginId = "roslyn.workbench.core",
        DisplayName = "Roslyn Workbench Core",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    /// <summary>
    /// Registers bundled first-party tools.
    /// </summary>
    /// <param name="registry">The plugin registry.</param>
    public void Register(IPluginRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        BundledCoreToolRegistrar.RegisterAll(registry);
    }
}
