namespace Roslyn.Workbench.Mcp.Plugins;

internal interface IPluginToolRegistrationMaterializer
{
    PluginMaterializationResult Materialize(PluginPreparationResult preparation);
}
