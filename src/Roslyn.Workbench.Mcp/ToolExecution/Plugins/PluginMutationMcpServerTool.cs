using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginMutationMcpServerTool<TRequest> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly RegisteredTool _tool;
    private readonly IMutationToolHandler<TRequest> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;

    public PluginMutationMcpServerTool(
        PluginMutationRegistration<TRequest> registration,
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory,
        IOptions<StartupOptions> options)
        : base(protocolFactory.CreatePluginTool<TRequest>(
            registration.Tool,
            options.Value.ToolOutputSchemaMode))
    {
        _tool = registration.Tool;
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
            if (!TryBindRequest(arguments, out TRequest? boundRequest, out var rejection))
            {
                return rejection;
            }

            request = boundRequest;
        }

        return await InvokeBoundRequestAsync(request, cancellationToken);
    }

    private async ValueTask<CallToolResult> InvokeBoundRequestAsync(TRequest request, CancellationToken cancellationToken)
    {
        PluginMutationExecutionLease contextLease;
        using (StartPhase(WorkbenchPerformanceEventSource.ContextAcquisitionPhase))
        {
            contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
        }

        await using var _ = contextLease;
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        PluginExecutionResult<MutationCandidate> proposalResult;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken);
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
            var noChange = PluginExecutionResult<MutationData>.NoChange(
                diagnostics: proposalResult.Diagnostics,
                warnings: proposalResult.Warnings);
            return CreateStructuredResult(McpPublishedResultSerializer.SerializePluginMutation(noChange), isError: false);
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
                McpPublishedResultSerializer.SerializePluginMutation(stagedResult),
                stagedResult.HasError);
        }
    }
}
