using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionMutationMcpServerTool<THandler, TRequest> : McpServerToolBase
    where THandler : class, ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    private readonly CodeActionToolMetadata _metadata;
    private readonly THandler _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

    public CodeActionMutationMcpServerTool(
        CodeActionMutationRegistration<THandler, TRequest> registration,
        THandler handler,
        ICodeActionExecutionContextFactory contextFactory,
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

        if (proposalResult.Outcome.IsError())
        {
            var failure = new CodeActionExecutionFailure
            {
                Outcome = proposalResult.Outcome,
                Error = proposalResult.Error
                    ?? throw new InvalidOperationException("Code Action mutation failure must provide an error."),
                RequiredAction = proposalResult.RequiredAction,
            };
            return CreateStructuredResult(McpPublishedResultSerializer.SerializeCodeActionFailure(failure), isError: true);
        }

        if (proposalResult.Outcome == CodeActionExecutionOutcome.NoChange || proposalResult.Data is null)
        {
            var noChange = CodeActionExecutionResult<MutationData>.NoChange(
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

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializeCodeActionMutation(stagedResult),
                stagedResult.Outcome.IsError());
        }
    }
}
