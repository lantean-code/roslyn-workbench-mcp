namespace Roslyn.Workbench.Mcp;

internal sealed record PreparedCatalogPlugin
{
    public PluginMetadata Metadata { get; init; } = new();

    public PluginPreparationResult Preparation { get; init; } = new();
}
