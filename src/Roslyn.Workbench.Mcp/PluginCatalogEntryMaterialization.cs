using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed record PluginCatalogEntryMaterialization
{
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public required PluginStatus Status { get; init; }
}
