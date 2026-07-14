namespace Roslyn.Workbench.Mcp.Plugins;

internal interface IPluginHandlerWarningInspector
{
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
