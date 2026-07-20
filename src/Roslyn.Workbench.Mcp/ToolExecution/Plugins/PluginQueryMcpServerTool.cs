using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginQueryMcpServerTool<TRequest, TResponse> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly IQueryToolHandler<TRequest, TResponse> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;

    public PluginQueryMcpServerTool(
        PluginQueryRegistration<TRequest, TResponse> registration,
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreatePluginTool<TRequest>(
            registration.Tool,
            options.Value.ToolOutputSchemaMode))
    {
        _handler = registration.Handler;
        _contextFactory = contextFactory;
    }

    protected override async ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        TRequest request;
        using (StartPhase(WorkbenchPerformanceEventSource.RequestBindingPhase))
        {
            request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        }

        ToolExecutionContextLease<IQueryContext> contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateQueryContext(request, cancellationToken);
        }

        await using var _ = contextLease;
        if (contextLease.HasShortCircuitResult)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.ShortCircuitResult),
                isError: true);
        }

        var context = contextLease.Context;
        PluginExecutionResult<TResponse> result;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            result = await _handler.ExecuteAsync(request, context, cancellationToken);
        }

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginQuery(result),
                result.Outcome.IsError());
        }
    }
}
