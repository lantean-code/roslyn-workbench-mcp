namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal interface IPluginToolRegistrationVisitor<out TResult>
{
    TResult VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest;

    TResult VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest;
}
