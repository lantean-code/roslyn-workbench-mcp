namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal interface IPluginHandlerTypeInspector
{
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
