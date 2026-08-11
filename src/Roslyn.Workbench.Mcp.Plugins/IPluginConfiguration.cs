namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Records the handler types and plugin-owned services supplied by one plugin during startup.
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Gets the plugin-owned singleton service registrations.
    /// </summary>
    IPluginServiceConfiguration Services { get; }

    /// <summary>
    /// Adds a query handler type to the plugin.
    /// </summary>
    /// <typeparam name="THandler">The query handler implementation type.</typeparam>
    /// <returns>A fluent metadata builder for the configured tool.</returns>
    QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler;

    /// <summary>
    /// Adds a mutation handler type to the plugin.
    /// </summary>
    /// <typeparam name="THandler">The mutation handler implementation type.</typeparam>
    /// <returns>A fluent metadata builder for the configured tool.</returns>
    MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler;
}
