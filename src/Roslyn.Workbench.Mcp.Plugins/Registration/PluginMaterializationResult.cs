namespace Roslyn.Workbench.Mcp.Plugins.Registration;

internal sealed record PluginMaterializationResult
{
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
