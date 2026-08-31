using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

/// <summary>
/// Runs a plugin query within an isolated workspace lease and publishes its MCP result.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class PluginQueryMcpServerTool<TRequest, TResponse> : McpServerToolBase<TRequest>
    where TRequest : WorkspaceBoundRequest
    where TResponse : IQueryResponse
{
    private readonly IQueryToolHandler<TRequest, TResponse> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly string _pluginId;
    private readonly string _toolName;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryMcpServerTool{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="registration">The query contract, handler, and catalogue metadata.</param>
    /// <param name="contextFactory">The factory that acquires workspace-scoped query contexts.</param>
    /// <param name="protocolFactory">The factory that creates the published MCP tool definition.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="options">The Host settings that control schema publication.</param>
    public PluginQueryMcpServerTool(
        PluginQueryRegistration<TRequest, TResponse> registration,
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreatePluginTool<TRequest>(
            registration.Tool,
            options.Value.ToolOutputSchemaMode),
            requestBinder)
    {
        _handler = registration.Handler;
        _contextFactory = contextFactory;
        _pluginId = registration.Tool.Plugin.PluginId;
        _toolName = registration.Tool.Metadata.Name;
    }

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        ToolExecutionContextLease<IQueryContext> contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateQueryContext(
                request,
                _pluginId,
                _toolName,
                cancellationToken);
        }

        await using var contextLeaseDisposal = contextLease;
        if (contextLease.HasShortCircuitResult)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.ShortCircuitResult),
                isError: true);
        }

        var context = contextLease.Context;
        try
        {
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
                    McpPublishedResultSerializer.SerializePluginQuery(
                        result,
                        context.Snapshot),
                    result.HasError);
            }
        }
        catch (Exception exception)
        {
            var workspaceContext = new CapturedWorkspaceContext(
                context.WorkspaceIdentity,
                context.CurrentSolution,
                context.TransactionRevision);

            throw new WorkspaceAttributedToolException(
                workspaceContext,
                exception);
        }
    }
}
