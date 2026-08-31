namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

/// <summary>
/// Validates frozen plugin configuration and projects it into materialization-ready tools and services.
/// </summary>
internal interface IPluginConfigurationPreparer
{
    /// <summary>
    /// Prepares one plugin's configuration using the selected contract accessibility policy.
    /// </summary>
    /// <param name="pluginMetadata">The validated plugin identity and version metadata.</param>
    /// <param name="configuration">The frozen tool and service configuration.</param>
    /// <param name="contractAccessibility">The handler-contract accessibility policy for the loading mode.</param>
    /// <returns>Prepared definitions and all configuration diagnostics.</returns>
    PluginPreparationResult Prepare(
        PluginMetadata pluginMetadata,
        PluginConfiguration configuration,
        PluginContractAccessibility contractAccessibility);
}
