namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginCatalogEntryMaterializer
{
    PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin);
}
