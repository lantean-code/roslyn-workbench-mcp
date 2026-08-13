namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginQueryRegistration<TRequest, TResponse> : IRegisteredPluginTool
    where TRequest : WorkspaceBoundRequest
    where TResponse : IQueryResponse
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
        return visitor.VisitQuery(this);
    }
}
