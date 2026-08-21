using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionQueryMcpServerTool<THandler, TRequest, TResponse> : McpServerToolBase<TRequest>
    where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
    where TRequest : WorkspaceBoundRequest
{
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

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
