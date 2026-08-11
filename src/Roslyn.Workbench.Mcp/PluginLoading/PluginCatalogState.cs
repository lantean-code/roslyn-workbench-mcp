namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogState : IPluginCatalogState, IDisposable, IAsyncDisposable
{
    private PluginRuntimeCatalogSnapshot _current = PluginRuntimeCatalogSnapshot.Empty;

    public PluginRuntimeCatalogSnapshot Current => Volatile.Read(ref _current);

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

    public void Dispose()
    {
        var current = TakeCurrent();

        if (ReferenceEquals(current, PluginRuntimeCatalogSnapshot.Empty))
        {
            return;
        }

        current.Catalog.Dispose();
    }

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
