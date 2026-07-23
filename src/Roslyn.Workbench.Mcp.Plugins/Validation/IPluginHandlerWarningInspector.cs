namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal interface IPluginHandlerWarningInspector
{
    IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType);
}
