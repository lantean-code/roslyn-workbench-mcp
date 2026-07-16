namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginEntryPointValidator
{
    string? GetValidationError(PluginEntryPointMetadata entryPoint);
}
