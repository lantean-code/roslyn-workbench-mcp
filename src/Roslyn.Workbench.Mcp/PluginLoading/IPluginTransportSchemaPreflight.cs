namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginTransportSchemaPreflight
{
    PluginTransportSchemaPreflightResult Preflight(
        IReadOnlyList<PreparedPluginTool> tools);
}
