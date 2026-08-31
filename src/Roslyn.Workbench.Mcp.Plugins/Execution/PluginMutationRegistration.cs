namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Binds a materialized mutation tool to its strongly typed handler.
/// </summary>
/// <typeparam name="TRequest">The mutation request type.</typeparam>
internal sealed class PluginMutationRegistration<TRequest> : IRegisteredPluginTool
    where TRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMutationRegistration{TRequest}"/> class.
    /// </summary>
    /// <param name="tool">The materialized tool metadata.</param>
    /// <param name="handler">The plugin handler instance.</param>
    public PluginMutationRegistration(
        RegisteredTool tool,
        IMutationToolHandler<TRequest> handler)
    {
        Tool = tool;
        Handler = handler;
    }

    /// <inheritdoc/>
    public RegisteredTool Tool { get; }

    /// <summary>
    /// Gets the strongly typed mutation handler.
    /// </summary>
    public IMutationToolHandler<TRequest> Handler { get; }

    /// <inheritdoc/>
    public TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitMutation(this);
    }
}
