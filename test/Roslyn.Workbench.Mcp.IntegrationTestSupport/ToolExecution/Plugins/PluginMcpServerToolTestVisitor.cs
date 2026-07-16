using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Protocol;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport.ToolExecution.Plugins;

internal sealed class PluginMcpServerToolTestVisitor : IPluginToolRegistrationVisitor<McpServerToolBase>
{
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly IMcpToolProtocolFactory _protocolFactory;
    private readonly IOptions<StartupOptions> _options;

    public PluginMcpServerToolTestVisitor(
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        ToolOutputSchemaMode outputSchemaMode)
    {
        _contextFactory = contextFactory;
        _protocolFactory = protocolFactory;
        _options = Options.Create(new StartupOptions
        {
            ToolOutputSchemaMode = outputSchemaMode,
        });
    }

    public McpServerToolBase VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginQueryMcpServerTool<TRequest, TResponse>(
            registration,
            _contextFactory,
            _protocolFactory,
            _options);
    }

    public McpServerToolBase VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest
    {
        return new PluginMutationMcpServerTool<TRequest>(
            registration,
            _contextFactory,
            _protocolFactory,
            _options);
    }
}
