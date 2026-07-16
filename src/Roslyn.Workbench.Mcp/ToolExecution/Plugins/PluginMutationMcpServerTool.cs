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
        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        if (contextLease.HasFailure)
        {
            return CreateStructuredResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context;
        var proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        if (proposalResult.Outcome.IsError())
        {
            var failure = new ToolExecutionFailureResult
            {
                Outcome = proposalResult.Outcome,
                Error = proposalResult.Error
                    ?? throw new InvalidOperationException("Plugin mutation failure must provide an error."),
                RequiredAction = proposalResult.RequiredAction,
                Diagnostics = proposalResult.Diagnostics,
                Warnings = proposalResult.Warnings,
            };
            return CreateStructuredResult(McpPublishedResultSerializer.SerializePluginFailure(failure), isError: true);
        }

        if (proposalResult.Outcome == PluginExecutionOutcome.NoChange || proposalResult.Data is null)
        {
            var noChange = PluginExecutionResult<MutationData>.NoChange(
                diagnostics: proposalResult.Diagnostics,
                warnings: proposalResult.Warnings);
            return CreateStructuredResult(McpPublishedResultSerializer.SerializePluginMutation(noChange), isError: false);
        }

        var stagedResult = await contextLease.StageAsync(
            _tool.Metadata.Name,
            proposalResult.Data,
            proposalResult.Diagnostics,
            proposalResult.Warnings,
            cancellationToken).ConfigureAwait(false);
        return CreateStructuredResult(
            McpPublishedResultSerializer.SerializePluginMutation(stagedResult),
            stagedResult.Outcome.IsError());
    }
}
