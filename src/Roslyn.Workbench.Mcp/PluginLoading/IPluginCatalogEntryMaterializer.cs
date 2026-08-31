namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Converts one prepared plugin into runtime catalogue entries and published tool wrappers.
/// </summary>
internal interface IPluginCatalogEntryMaterializer
{
    /// <summary>
    /// Materializes one validated plugin for runtime publication.
    /// </summary>
    /// <param name="plugin">The plugin instance being registered or inspected.</param>
    /// <returns>The plugin status and runtime tools, or the reason materialization failed.</returns>
    PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin);
}
