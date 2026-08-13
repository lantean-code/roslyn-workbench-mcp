using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginMcpServerToolFactory : IPluginMcpServerToolFactory
{
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly IMcpToolProtocolFactory _protocolFactory;
    private readonly IToolRequestBinder _requestBinder;
    private readonly IOptions<StartupOptions> _options;

    public PluginMcpServerToolFactory(
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IOptions<StartupOptions> options)
    {
        _contextFactory = contextFactory;
        _protocolFactory = protocolFactory;
        _requestBinder = requestBinder;
        _options = options;
    }

    public McpServerTool VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
        where TResponse : IQueryResponse
    {
        return new PluginQueryMcpServerTool<TRequest, TResponse>(
            registration,
            _contextFactory,
            _protocolFactory,
            _requestBinder,
            _options);
    }

    public McpServerTool VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceMutationRequest
    {
        return new PluginMutationMcpServerTool<TRequest>(
            registration,
            _contextFactory,
            _protocolFactory,
            _requestBinder,
            _options);
    }
}
