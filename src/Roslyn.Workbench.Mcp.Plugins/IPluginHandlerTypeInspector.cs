namespace Roslyn.Workbench.Mcp.Plugins;

internal interface IPluginHandlerTypeInspector
{
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
