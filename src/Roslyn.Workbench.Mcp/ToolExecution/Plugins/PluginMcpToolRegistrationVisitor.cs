namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginMcpToolRegistrationVisitor : IPluginToolRegistrationVisitor<bool>
{
    private readonly IServiceCollection _services;

    public PluginMcpToolRegistrationVisitor(IServiceCollection services)
    {
        _services = services;
    }

    public bool VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<McpServerTool, PluginQueryMcpServerTool<TRequest, TResponse>>();
        return true;
    }

    public bool VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceMutationRequest
    {
        _services.AddSingleton(registration);
        _services.AddSingleton<McpServerTool, PluginMutationMcpServerTool<TRequest>>();
        return true;
    }
}
