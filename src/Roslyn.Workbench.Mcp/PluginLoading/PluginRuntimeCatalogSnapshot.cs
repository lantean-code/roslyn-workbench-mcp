namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Captures an immutable snapshot of plugin runtime catalog.
/// </summary>
internal sealed record PluginRuntimeCatalogSnapshot
{
    /// <summary>
    /// Gets the unpublished catalogue sentinel used before plugin startup completes.
    /// </summary>
    public static PluginRuntimeCatalogSnapshot Empty { get; } = new();

    /// <summary>
    /// Gets the public plugin catalogue metadata exposed by server status.
    /// </summary>
    public PluginCatalogSnapshot Catalog { get; init; } = new();

    /// <summary>
    /// Gets the executable MCP tools indexed by their published names.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerTool> Tools { get; init; } =
        new Dictionary<string, McpServerTool>(StringComparer.Ordinal);
}
