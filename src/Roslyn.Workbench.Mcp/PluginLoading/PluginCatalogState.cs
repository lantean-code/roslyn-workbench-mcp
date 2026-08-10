namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogState : IPluginCatalogState
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
}
