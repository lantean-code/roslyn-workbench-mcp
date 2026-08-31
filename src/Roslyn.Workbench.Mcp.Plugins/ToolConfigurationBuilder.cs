namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Supplies fluent metadata overrides for one configured plugin tool.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
public abstract class ToolConfigurationBuilder<TBuilder> : IToolConfigurationBuilderState where TBuilder : ToolConfigurationBuilder<TBuilder>
{
    private bool _isFrozen;

    /// <inheritdoc cref="IToolConfigurationBuilderState.Name"/>
    internal string? Name { get; private set; }

    /// <inheritdoc cref="IToolConfigurationBuilderState.Title"/>
    internal string? Title { get; private set; }

    /// <inheritdoc cref="IToolConfigurationBuilderState.Description"/>
    internal string? Description { get; private set; }

    /// <inheritdoc cref="IToolConfigurationBuilderState.ResultSummary"/>
    internal string? ResultSummary { get; private set; }

    /// <inheritdoc cref="IToolConfigurationBuilderState.Destructive"/>
    internal virtual bool? Destructive => null;

    bool? IToolConfigurationBuilderState.Destructive => Destructive;

    string? IToolConfigurationBuilderState.Name => Name;

    string? IToolConfigurationBuilderState.Title => Title;

    string? IToolConfigurationBuilderState.Description => Description;

    string? IToolConfigurationBuilderState.ResultSummary => ResultSummary;

    /// <summary>
    /// Overrides the MCP tool name.
    /// </summary>
    /// <param name="name">The globally unique tool name, containing 1 to 128 ASCII letters, digits, underscores, hyphens, or periods.</param>
    /// <returns>The same builder.</returns>
    public TBuilder WithName(string name)
    {
        EnsureMutable();
        Name = name;
        return (TBuilder)this;
    }

    /// <summary>
    /// Overrides the tool title.
    /// </summary>
    /// <param name="title">The title displayed to users.</param>
    /// <returns>The same builder.</returns>
    public TBuilder WithTitle(string title)
    {
        EnsureMutable();
        Title = title;
        return (TBuilder)this;
    }

    /// <summary>
    /// Overrides the tool description.
    /// </summary>
    /// <param name="description">The tool description.</param>
    /// <returns>The same builder.</returns>
    public TBuilder WithDescription(string description)
    {
        EnsureMutable();
        Description = description;
        return (TBuilder)this;
    }

    /// <summary>
    /// Overrides the optional concise result summary.
    /// </summary>
    /// <param name="resultSummary">The user-facing summary reported when the tool completes.</param>
    /// <returns>The same builder.</returns>
    public TBuilder WithResultSummary(string resultSummary)
    {
        EnsureMutable();
        ResultSummary = resultSummary;
        return (TBuilder)this;
    }

    /// <inheritdoc cref="IToolConfigurationBuilderState.Freeze"/>
    internal void Freeze()
    {
        _isFrozen = true;
    }

    void IToolConfigurationBuilderState.Freeze()
    {
        Freeze();
    }

    /// <summary>
    /// Verifies that the builder is still within the plugin configuration phase.
    /// </summary>
    protected void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new InvalidOperationException("Plugin tool configuration cannot be changed after Configure returns.");
        }
    }
}
