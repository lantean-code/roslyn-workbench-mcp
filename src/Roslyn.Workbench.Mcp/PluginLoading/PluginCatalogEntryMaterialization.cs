namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginCatalogEntryMaterialization
{
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public required PluginStatus Status { get; init; }

    public IDisposable? ServiceProviderLifetime { get; init; }
}
