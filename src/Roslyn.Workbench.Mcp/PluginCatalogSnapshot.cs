using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed record PluginCatalogSnapshot
{
    public IReadOnlyList<RegisteredPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<PluginStatus> Plugins { get; init; } = [];
}
