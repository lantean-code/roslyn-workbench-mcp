using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionMutationMcpServerTool<THandler, TRequest> : McpServerToolBase<TRequest>
    where THandler : class, ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    private readonly CodeActionToolMetadata _metadata;
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;
    private readonly ICodeActionReferenceStore _referenceStore;

    public CodeActionMutationMcpServerTool(
        CodeActionMutationRegistration<THandler, TRequest> registration,
        THandler handler,
        ICodeActionExecutionContextFactory contextFactory,
        ICodeActionReferenceStore referenceStore,
        IMcpToolProtocolFactory protocolFactory,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreateCodeActionTool<TRequest>(
            registration.Metadata,
            registration.Kind,
            registration.ResponseType,
            options.Value.ToolOutputSchemaMode))
    {
        _metadata = registration.Metadata;
        _handler = handler;
        _contextFactory = contextFactory;
        _referenceStore = referenceStore;
    }

    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        CodeActionMutationExecutionLease contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
        }

        await using var _ = contextLease;
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializeCodeActionFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        CodeActionExecutionResult<WorkspaceMutationCandidate> proposalResult;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken);
        }

        if (proposalResult.HasError)
        {
            var failure = new CodeActionExecutionFailure
            {
                Outcome = proposalResult.Outcome,
                Error = proposalResult.Error,
                RequiredAction = proposalResult.RequiredAction,
                Diagnostics = proposalResult.Diagnostics,
                Warnings = proposalResult.Warnings,
            };

            return CreateStructuredResult(McpPublishedResultSerializer.SerializeCodeActionFailure(failure), isError: true);
        }

        if (!proposalResult.IsSucceeded)
        {
            var noChange = CodeActionExecutionResult.NoChange<MutationData>(
                diagnostics: proposalResult.Diagnostics,
                warnings: proposalResult.Warnings);

            return CreateStructuredResult(McpPublishedResultSerializer.SerializeCodeActionMutation(noChange), isError: false);
        }

        CodeActionExecutionResult<MutationData> stagedResult;
        using (StartPhase(WorkbenchPerformanceEventSource.MutationStagingPhase))
        {
            stagedResult = await contextLease.StageAsync(
                _metadata.Name,
                proposalResult.Data,
                proposalResult.Diagnostics,
                proposalResult.Warnings,
                cancellationToken);
        }

        if (stagedResult.IsSucceeded && request is ICodeActionReferenceRequest referenceRequest)
        {
            _referenceStore.Remove(referenceRequest.ActionId);
        }

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializeCodeActionMutation(stagedResult),
                stagedResult.HasError);
        }
    }
}
