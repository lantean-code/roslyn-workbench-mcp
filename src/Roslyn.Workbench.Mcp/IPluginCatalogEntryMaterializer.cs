namespace Roslyn.Workbench.Mcp;

internal interface IPluginCatalogEntryMaterializer
{
    PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin);
}
