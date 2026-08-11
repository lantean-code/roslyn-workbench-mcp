namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

internal sealed record PluginPreparationResult
{
    public IReadOnlyList<PreparedPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<PluginServiceDefinition> Services { get; init; } = [];

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
