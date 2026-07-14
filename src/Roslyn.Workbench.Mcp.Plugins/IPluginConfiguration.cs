namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Records the handler types supplied by one plugin during startup.
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Adds a query handler type to the plugin.
    /// </summary>
    /// <typeparam name="THandler">The query handler implementation type.</typeparam>
    /// <returns>A fluent metadata builder for the configured tool.</returns>
    QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler, new();

    /// <summary>
    /// Adds a mutation handler type to the plugin.
    /// </summary>
    /// <typeparam name="THandler">The mutation handler implementation type.</typeparam>
    /// <returns>A fluent metadata builder for the configured tool.</returns>
    MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler, new();
}
