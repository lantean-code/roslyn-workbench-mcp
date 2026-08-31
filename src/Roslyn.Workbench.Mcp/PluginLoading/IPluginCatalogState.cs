namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Publishes the immutable runtime plugin catalogue after startup loading completes.
/// </summary>
internal interface IPluginCatalogState
{
    /// <summary>
    /// Gets the current runtime catalogue, or the initial empty catalogue before publication.
    /// </summary>
    PluginRuntimeCatalogSnapshot Current { get; }

    /// <summary>
    /// Replaces the current runtime catalogue with the startup snapshot.
    /// </summary>
    /// <param name="snapshot">The runtime plugin and tool lookup to publish.</param>
    void Publish(PluginRuntimeCatalogSnapshot snapshot);
}
