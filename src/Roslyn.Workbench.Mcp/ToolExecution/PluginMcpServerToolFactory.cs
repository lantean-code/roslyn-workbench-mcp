using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class PluginMcpServerToolFactory : IPluginToolRegistrationVisitor<McpServerToolBase>
{
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public PluginMcpServerToolFactory(
        IToolExecutionContextFactory contextFactory,
        ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        _contextFactory = contextFactory;
        _outputSchemaMode = outputSchemaMode;
    }

    public McpServerToolBase VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginQueryMcpServerTool<TRequest, TResponse>(
            McpToolProtocolFactory.CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Handler,
            _contextFactory);
    }

    public McpServerToolBase VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginMutationMcpServerTool<TRequest>(
            McpToolProtocolFactory.CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Tool,
            registration.Handler,
            _contextFactory);
    }
}
