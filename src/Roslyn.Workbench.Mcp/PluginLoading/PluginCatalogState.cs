namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Publishes the runtime plugin catalogue exactly once and owns its shutdown disposal.
/// </summary>
internal sealed class PluginCatalogState : IPluginCatalogState, IDisposable, IAsyncDisposable
{
    private PluginRuntimeCatalogSnapshot _current = PluginRuntimeCatalogSnapshot.Empty;

    /// <summary>
    /// Gets the immutable plugin catalogue published for this host instance.
    /// </summary>
    public PluginRuntimeCatalogSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// Publishes the plugin runtime catalogue exactly once for this host instance.
    /// </summary>
    /// <param name="snapshot">The completed plugin runtime catalogue to publish.</param>
    public void Publish(PluginRuntimeCatalogSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, PluginRuntimeCatalogSnapshot.Empty))
        {
            throw new InvalidOperationException("The unpublished plugin runtime catalogue sentinel cannot be published.");
        }

        var previous = Interlocked.CompareExchange(
            ref _current,
            snapshot,
            PluginRuntimeCatalogSnapshot.Empty);

        if (!ReferenceEquals(previous, PluginRuntimeCatalogSnapshot.Empty))
        {
            throw new InvalidOperationException("The plugin runtime catalogue has already been published for this Host instance.");
        }
    }

    /// <summary>
    /// Removes the published catalogue and disposes its plugin service providers.
    /// </summary>
    public void Dispose()
    {
        var current = TakeCurrent();

        if (ReferenceEquals(current, PluginRuntimeCatalogSnapshot.Empty))
        {
            return;
        }

        current.Catalog.Dispose();
    }

    /// <summary>
    /// Removes the published catalogue and asynchronously disposes its plugin service providers.
    /// </summary>
    /// <returns>A task that completes after the catalogue has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        var current = TakeCurrent();

        if (ReferenceEquals(current, PluginRuntimeCatalogSnapshot.Empty))
        {
            return;
        }

        await current.Catalog.DisposeAsync();
    }

    private PluginRuntimeCatalogSnapshot TakeCurrent()
    {
        return Interlocked.Exchange(
            ref _current,
            PluginRuntimeCatalogSnapshot.Empty);
    }
}
