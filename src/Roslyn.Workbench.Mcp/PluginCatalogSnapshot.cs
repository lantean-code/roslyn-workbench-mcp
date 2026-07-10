using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed record PluginCatalogSnapshot
{
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<PluginStatus> Plugins { get; init; } = [];
}
