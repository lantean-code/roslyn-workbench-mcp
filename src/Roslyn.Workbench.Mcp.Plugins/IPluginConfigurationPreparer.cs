namespace Roslyn.Workbench.Mcp.Plugins;

internal interface IPluginConfigurationPreparer
{
    PluginPreparationResult Prepare(PluginMetadata pluginMetadata, PluginConfiguration configuration);
}
