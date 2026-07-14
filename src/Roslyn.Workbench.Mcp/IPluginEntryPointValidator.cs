namespace Roslyn.Workbench.Mcp;

internal interface IPluginEntryPointValidator
{
    string? GetValidationError(PluginEntryPointMetadata entryPoint);
}
