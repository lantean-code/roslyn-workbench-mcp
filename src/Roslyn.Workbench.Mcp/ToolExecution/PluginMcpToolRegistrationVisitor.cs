using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class PluginMcpToolRegistrationVisitor : IPluginToolRegistrationVisitor<bool>
{
    private readonly IServiceCollection _services;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public PluginMcpToolRegistrationVisitor(
        IServiceCollection services,
        ToolOutputSchemaMode outputSchemaMode)
    {
        _services = services;
        _outputSchemaMode = outputSchemaMode;
    }

    public bool VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton<McpServerTool>(serviceProvider => new PluginQueryMcpServerTool<TRequest, TResponse>(
            serviceProvider.GetRequiredService<IMcpToolProtocolFactory>()
                .CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Handler,
            serviceProvider.GetRequiredService<IToolExecutionContextFactory>()));
        return true;
    }

    public bool VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        _services.AddSingleton<McpServerTool>(serviceProvider => new PluginMutationMcpServerTool<TRequest>(
            serviceProvider.GetRequiredService<IMcpToolProtocolFactory>()
                .CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Tool,
            registration.Handler,
            serviceProvider.GetRequiredService<IToolExecutionContextFactory>()));
        return true;
    }
}
