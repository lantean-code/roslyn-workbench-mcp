using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginMutationMcpServerTool<TRequest> : McpServerToolBase<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    private readonly RegisteredTool _tool;
    private readonly IMutationToolHandler<TRequest> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;

    public PluginMutationMcpServerTool(
        PluginMutationRegistration<TRequest> registration,
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreatePluginTool<TRequest>(
            registration.Tool,
            options.Value.ToolOutputSchemaMode),
            requestBinder)
    {
        _tool = registration.Tool;
        _handler = registration.Handler;
        _contextFactory = contextFactory;
    }

    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        PluginMutationExecutionLease contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
        }

        await using var contextLeaseDisposal = contextLease;
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        try
        {
            PluginExecutionResult<MutationCandidate> proposalResult;
            ToolExecutionFailureResult? containmentFailure;
            using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
            {
                try
                {
                    proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken);
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

            if (proposalResult.HasError)
            {
                var failure = new ToolExecutionFailureResult
                {
                    Outcome = proposalResult.Outcome,
                    Error = proposalResult.Error,
                    RequiredAction = proposalResult.RequiredAction,
                    Diagnostics = proposalResult.Diagnostics,
                    Warnings = proposalResult.Warnings,
                };

                return CreateStructuredResult(McpPublishedResultSerializer.SerializePluginFailure(failure), isError: true);
            }

            if (!proposalResult.IsSucceeded)
            {
                var noChange = PluginExecutionResult.NoChange<MutationData>(
                    diagnostics: proposalResult.Diagnostics,
                    warnings: proposalResult.Warnings);

                return CreateStructuredResult(
                    McpPublishedResultSerializer.SerializePluginMutation(
                        noChange,
                        context.Snapshot),
                    isError: false);
            }

            PluginExecutionResult<MutationData> stagedResult;
            using (StartPhase(WorkbenchPerformanceEventSource.MutationStagingPhase))
            {
                stagedResult = await contextLease.StageAsync(
                    _tool.Metadata.Name,
                    proposalResult.Data,
                    proposalResult.Diagnostics,
                    proposalResult.Warnings,
                    cancellationToken);
            }

            using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
            {
                return CreateStructuredResult(
                    McpPublishedResultSerializer.SerializePluginMutation(
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
