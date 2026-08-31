namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Binds a materialized query tool to its strongly typed handler.
/// </summary>
/// <typeparam name="TRequest">The query request type.</typeparam>
/// <typeparam name="TResponse">The query response type.</typeparam>
internal sealed class PluginQueryRegistration<TRequest, TResponse> : IRegisteredPluginTool
    where TRequest : WorkspaceBoundRequest
    where TResponse : IQueryResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryRegistration{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="tool">The materialized tool metadata.</param>
    /// <param name="handler">The plugin handler instance.</param>
    public PluginQueryRegistration(
        RegisteredTool tool,
        IQueryToolHandler<TRequest, TResponse> handler)
    {
        Tool = tool;
        Handler = handler;
    }

    /// <inheritdoc/>
    public RegisteredTool Tool { get; }

    /// <summary>
    /// Gets the strongly typed query handler.
    /// </summary>
    public IQueryToolHandler<TRequest, TResponse> Handler { get; }

    /// <inheritdoc/>
    public TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitQuery(this);
    }
}
