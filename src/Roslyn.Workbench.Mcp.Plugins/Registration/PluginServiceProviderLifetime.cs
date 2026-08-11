namespace Roslyn.Workbench.Mcp.Plugins.Registration;

internal sealed class PluginServiceProviderLifetime : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public PluginServiceProviderLifetime(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        return _serviceProvider.DisposeAsync();
    }
}
