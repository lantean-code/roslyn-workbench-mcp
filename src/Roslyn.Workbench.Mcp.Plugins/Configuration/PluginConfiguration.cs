namespace Roslyn.Workbench.Mcp.Plugins.Configuration;

internal sealed class PluginConfiguration : IPluginConfiguration
{
    private readonly List<ConfiguredToolDefinition> _definitions = [];
    private bool _isFrozen;

    public IReadOnlyList<ConfiguredToolDefinition> Definitions => _definitions;

    public QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler, new()
    {
        EnsureMutable();
        var builder = new QueryToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Query, static () => new THandler(), builder);
        return builder;
    }

    public MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler, new()
    {
        EnsureMutable();
        var builder = new MutationToolConfigurationBuilder();
        AddTool(typeof(THandler), ToolKind.Mutation, static () => new THandler(), builder);
        return builder;
    }

    private void AddTool(
        Type handlerType,
        ToolKind kind,
        Func<object> handlerFactory,
        IToolConfigurationBuilderState builder)
    {
        _definitions.Add(new ConfiguredToolDefinition
        {
            HandlerType = handlerType,
            HandlerFactory = handlerFactory,
            Kind = kind,
            Builder = builder,
        });
    }

    public void Freeze()
    {
        foreach (var definition in _definitions)
        {
            definition.Builder.Freeze();
        }

        _isFrozen = true;
    }

    private void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Plugin configuration cannot be changed after Configure returns.");
        }
    }
}
