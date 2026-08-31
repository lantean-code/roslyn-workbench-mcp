namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

/// <summary>
/// Collects singleton service mappings owned by one plugin during configuration.
/// </summary>
internal sealed class PluginServiceConfiguration : IPluginServiceConfiguration
{
    private readonly List<PluginServiceDefinition> _definitions = [];
    private bool _isFrozen;

    /// <summary>
    /// Gets the configured service mappings in registration order.
    /// </summary>
    public IReadOnlyList<PluginServiceDefinition> Definitions => _definitions;

    /// <inheritdoc/>
    public void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        AddDefinition(typeof(TService), typeof(TImplementation));
    }

    /// <inheritdoc/>
    public void AddSingleton<TImplementation>()
        where TImplementation : class
    {
        AddDefinition(typeof(TImplementation), typeof(TImplementation));
    }

    /// <summary>
    /// Prevents additional service registrations after plugin configuration returns.
    /// </summary>
    public void Freeze()
    {
        _isFrozen = true;
    }

    private void AddDefinition(Type serviceType, Type implementationType)
    {
        EnsureMutable();
        _definitions.Add(new PluginServiceDefinition
        {
            ServiceType = serviceType,
            ImplementationType = implementationType,
        });
    }

    private void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Plugin service configuration cannot be changed after Configure returns.");
        }
    }
}
