namespace Roslyn.Workbench.Mcp.Plugins.Registration;

/// <summary>
/// Owns a plugin service provider and preserves asynchronous disposal for its singleton services.
/// </summary>
internal sealed class PluginServiceProviderLifetime : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginServiceProviderLifetime"/> class.
    /// </summary>
    /// <param name="serviceProvider">The provider containing plugin services and handlers.</param>
    public PluginServiceProviderLifetime(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Synchronously completes asynchronous provider disposal for synchronous host shutdown paths.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously disposes the plugin service provider and its singleton services.
    /// </summary>
    /// <returns>A task representing provider disposal.</returns>
    public ValueTask DisposeAsync()
    {
        return _serviceProvider.DisposeAsync();
    }
}
