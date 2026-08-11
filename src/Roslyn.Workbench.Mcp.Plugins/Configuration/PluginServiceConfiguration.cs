namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal sealed class PluginServiceConfiguration : IPluginServiceConfiguration
{
    private readonly List<PluginServiceDefinition> _definitions = [];
    private bool _isFrozen;

    public IReadOnlyList<PluginServiceDefinition> Definitions => _definitions;

    public void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        AddDefinition(typeof(TService), typeof(TImplementation));
    }

    public void AddSingleton<TImplementation>()
        where TImplementation : class
    {
        AddDefinition(typeof(TImplementation), typeof(TImplementation));
    }

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
