namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

internal interface IPluginConfigurationPreparer
{
    PluginPreparationResult Prepare(PluginMetadata pluginMetadata, PluginConfiguration configuration);
}
