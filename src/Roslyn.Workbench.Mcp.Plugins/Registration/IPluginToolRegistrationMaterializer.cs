namespace Roslyn.Workbench.Mcp.Plugins.Registration;

internal interface IPluginToolRegistrationMaterializer
{
    PluginMaterializationResult Materialize(PluginPreparationResult preparation);
}
