namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

/// <summary>
/// Collects tool and service registrations during a plugin's synchronous configuration callback.
/// </summary>
internal sealed class PluginConfiguration : IPluginConfiguration
{
    private readonly List<ConfiguredToolDefinition> _definitions = [];
    private readonly PluginServiceConfiguration _services = new();
    private bool _isFrozen;

    /// <summary>
    /// Gets the configured tool definitions in registration order.
    /// </summary>
    public IReadOnlyList<ConfiguredToolDefinition> Definitions => _definitions;

    /// <inheritdoc/>
    public IPluginServiceConfiguration Services => _services;

    /// <summary>
    /// Gets the configured plugin service definitions in registration order.
    /// </summary>
    public IReadOnlyList<PluginServiceDefinition> ServiceDefinitions => _services.Definitions;

    /// <inheritdoc/>
    public QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler
    {
        EnsureMutable();
        var builder = new QueryToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Query, builder);
        return builder;
    }

    /// <inheritdoc/>
    public MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler
    {
        EnsureMutable();
        var builder = new MutationToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Mutation, builder);
        return builder;
    }

    /// <summary>
    /// Freezes every tool builder and the service collection after plugin configuration returns.
    /// </summary>
    public void Freeze()
    {
        foreach (var definition in _definitions)
        {
            definition.Builder.Freeze();
        }

        _services.Freeze();
        _isFrozen = true;
    }

    private void AddTool(
        Type handlerType,
        ToolKind kind,
        IToolConfigurationBuilderState builder)
    {
        _definitions.Add(new ConfiguredToolDefinition
        {
            HandlerType = handlerType,
            Kind = kind,
            Builder = builder,
        });
    }

    private void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Plugin configuration cannot be changed after Configure returns.");
        }
    }
}
