namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal sealed class PluginConfiguration : IPluginConfiguration
{
    private readonly List<ConfiguredToolDefinition> _definitions = [];
    private readonly PluginServiceConfiguration _services = new();
    private bool _isFrozen;

    public IReadOnlyList<ConfiguredToolDefinition> Definitions => _definitions;

    public IPluginServiceConfiguration Services => _services;

    public IReadOnlyList<PluginServiceDefinition> ServiceDefinitions => _services.Definitions;

    public QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler
    {
        EnsureMutable();
        var builder = new QueryToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Query, builder);
        return builder;
    }

    public MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler
    {
        EnsureMutable();
        var builder = new MutationToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Mutation, builder);
        return builder;
    }

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
