namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginCatalogState
{
    PluginRuntimeCatalogSnapshot Current { get; }

    void Publish(PluginRuntimeCatalogSnapshot snapshot);
}
