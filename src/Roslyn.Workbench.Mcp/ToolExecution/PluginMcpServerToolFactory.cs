using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class PluginMcpServerToolFactory : IPluginToolRegistrationVisitor<McpServerToolBase>
{
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly IMcpToolProtocolFactory _protocolFactory;
    private readonly ToolOutputSchemaMode _outputSchemaMode;

    public PluginMcpServerToolFactory(
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        _contextFactory = contextFactory;
        _protocolFactory = protocolFactory;
        _outputSchemaMode = outputSchemaMode;
    }

    public McpServerToolBase VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginQueryMcpServerTool<TRequest, TResponse>(
            _protocolFactory.CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Handler,
            _contextFactory);
    }

    public McpServerToolBase VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginMutationMcpServerTool<TRequest>(
            _protocolFactory.CreatePluginTool<TRequest>(registration.Tool, _outputSchemaMode),
            registration.Tool,
            registration.Handler,
            _contextFactory);
    }
}
