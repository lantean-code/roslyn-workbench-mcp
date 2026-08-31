using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

/// <summary>
/// Applies a referenced Code Action within the active transaction and publishes its staged result.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TRequest">The request type.</typeparam>
internal sealed class CodeActionMutationMcpServerTool<THandler, TRequest> : McpServerToolBase<TRequest>
    where THandler : class, ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    private readonly CodeActionToolMetadata _metadata;
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;
    private readonly ICodeActionReferenceStore _referenceStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionMutationMcpServerTool{THandler, TRequest}"/> class.
    /// </summary>
    /// <param name="registration">The mutation contract and catalogue metadata.</param>
    /// <param name="handler">The Code Action mutation handler invoked for each tool request.</param>
    /// <param name="contextFactory">The factory that acquires transaction-scoped Code Action contexts.</param>
    /// <param name="referenceStore">The store containing short-lived Code Action references returned by queries.</param>
    /// <param name="protocolFactory">The factory that creates the published MCP tool definition.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="options">The Host settings that control schema publication.</param>
    public CodeActionMutationMcpServerTool(
        CodeActionMutationRegistration<THandler, TRequest> registration,
        THandler handler,
        ICodeActionExecutionContextFactory contextFactory,
        ICodeActionReferenceStore referenceStore,
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
        _metadata = registration.Metadata;
        _handler = handler;
        _contextFactory = contextFactory;
        _referenceStore = referenceStore;
    }

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        CodeActionMutationExecutionLease contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
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

                var content = McpPublishedResultSerializer.SerializeCodeActionFailure(failure);
                return CreateStructuredResult(content, isError: true);
            }

            if (!proposalResult.IsSucceeded)
            {
                var noChange = CodeActionExecutionResult.NoChange<MutationData>(
                    diagnostics: proposalResult.Diagnostics,
                    warnings: proposalResult.Warnings);

                return CreateStructuredResult(
                    McpPublishedResultSerializer.SerializeCodeActionMutation(
                        noChange,
                        context.Snapshot),
                    isError: false);
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

            var shouldConsumeReference = stagedResult.IsSucceeded
                || stagedResult.Error?.Code == WorkspaceErrorCodes.MutationCandidateChanged;

            if (shouldConsumeReference
                && request is ICodeActionReferenceRequest referenceRequest)
            {
                _referenceStore.Remove(referenceRequest.ActionId);
            }

            using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
            {
                return CreateStructuredResult(
                    McpPublishedResultSerializer.SerializeCodeActionMutation(
                        stagedResult,
                        context.Snapshot),
                    stagedResult.HasError);
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
