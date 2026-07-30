using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginQueryMcpServerTool<TRequest, TResponse> : McpServerToolBase<TRequest>
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

    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        ToolExecutionContextLease<IQueryContext> contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateQueryContext(request, cancellationToken);
        }

        await using var contextLease;
        if (contextLease.HasShortCircuitResult)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.ShortCircuitResult),
                isError: true);
        }

        var context = contextLease.Context;
        PluginExecutionResult<TResponse> result;
        ToolExecutionFailureResult? containmentFailure;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            try
            {
                result = await _handler.ExecuteAsync(request, context, cancellationToken);
            }
            finally
            {
                containmentFailure = _contextFactory.DetectUnexpectedWorkspaceChange(context);
            }
        }

        if (containmentFailure is not null)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(containmentFailure),
                isError: true);
        }

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginQuery(result),
                result.HasError);
        }
    }
}
