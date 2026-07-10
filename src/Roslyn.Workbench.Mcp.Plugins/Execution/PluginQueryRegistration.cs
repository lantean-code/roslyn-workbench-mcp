namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginQueryRegistration<TRequest, TResponse> : IRegisteredPluginTool
    where TRequest : WorkspaceBoundRequest
{
    public PluginQueryRegistration(
        RegisteredTool tool,
        IQueryToolHandler<TRequest, TResponse> handler)
    {
        Tool = tool;
        Handler = handler;
    }

    public RegisteredTool Tool { get; }

    public IQueryToolHandler<TRequest, TResponse> Handler { get; }

    public TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);

        return visitor.VisitQuery(this);
    }
}
