namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PreparedCatalogPlugin
{
    public required PluginMetadata Metadata { get; init; }

    public PluginPreparationResult Preparation { get; init; } = new();
}
