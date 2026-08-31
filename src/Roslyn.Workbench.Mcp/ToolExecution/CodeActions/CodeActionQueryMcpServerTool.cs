using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

/// <summary>
/// Runs a Code Action query within a workspace lease and publishes its MCP result.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class CodeActionQueryMcpServerTool<THandler, TRequest, TResponse> : McpServerToolBase<TRequest>
    where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionQueryMcpServerTool{THandler, TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="registration">The query contract and catalogue metadata.</param>
    /// <param name="handler">The Code Action query handler invoked for each tool request.</param>
    /// <param name="contextFactory">The factory that acquires workspace-scoped Code Action contexts.</param>
    /// <param name="protocolFactory">The factory that creates the published MCP tool definition.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="options">The Host settings that control schema publication.</param>
    public CodeActionQueryMcpServerTool(
        CodeActionQueryRegistration<THandler, TRequest, TResponse> registration,
        THandler handler,
        ICodeActionExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreateCodeActionTool<TRequest>(
            registration.Metadata,
            registration.Kind,
            registration.ResponseType,
            options.Value.ToolOutputSchemaMode),
            requestBinder)
    {
        _handler = handler;
        _contextFactory = contextFactory;
    }

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        CodeActionQueryExecutionLease contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateQueryContext(request, cancellationToken);
        }

        await using var contextLeaseDisposal = contextLease;
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializeCodeActionFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        try
        {
            CodeActionExecutionResult<TResponse> result;
            using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
            {
                result = await _handler.ExecuteAsync(request, context, cancellationToken);
            }

            using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
            {
                return CreateStructuredResult(
                    McpPublishedResultSerializer.SerializeCodeActionQuery(
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
