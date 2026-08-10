namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginRuntimeCatalogSnapshot
{
    public static PluginRuntimeCatalogSnapshot Empty { get; } = new();

    public PluginCatalogSnapshot Catalog { get; init; } = new();

    public IReadOnlyDictionary<string, McpServerTool> Tools { get; init; } =
        new Dictionary<string, McpServerTool>(StringComparer.Ordinal);
}
