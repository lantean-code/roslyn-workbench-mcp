namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Configures singleton services owned by one plugin.
/// </summary>
public interface IPluginServiceConfiguration
{
    /// <summary>
    /// Adds a singleton service mapping to the plugin's isolated service provider.
    /// </summary>
    /// <typeparam name="TService">The service contract type.</typeparam>
    /// <typeparam name="TImplementation">The service implementation type.</typeparam>
    void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    /// <summary>
    /// Adds a concrete singleton service to the plugin's isolated service provider.
    /// </summary>
    /// <typeparam name="TImplementation">The service implementation type.</typeparam>
    void AddSingleton<TImplementation>()
        where TImplementation : class;
}
