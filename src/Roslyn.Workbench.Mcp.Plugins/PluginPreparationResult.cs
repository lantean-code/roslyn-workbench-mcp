namespace Roslyn.Workbench.Mcp.Plugins;

internal sealed record PluginPreparationResult
{
    public IReadOnlyList<PreparedPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
