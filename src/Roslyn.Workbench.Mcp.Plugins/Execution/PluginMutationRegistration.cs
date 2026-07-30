namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginMutationRegistration<TRequest> : IRegisteredPluginTool
    where TRequest : WorkspaceMutationRequest
{
    public PluginMutationRegistration(
        RegisteredTool tool,
        IMutationToolHandler<TRequest> handler)
    {
        Tool = tool;
        Handler = handler;
    }

    public RegisteredTool Tool { get; }

    public IMutationToolHandler<TRequest> Handler { get; }

    public TResult Accept<TResult>(IPluginToolRegistrationVisitor<TResult> visitor)
    {
        return visitor.VisitMutation(this);
    }
}
