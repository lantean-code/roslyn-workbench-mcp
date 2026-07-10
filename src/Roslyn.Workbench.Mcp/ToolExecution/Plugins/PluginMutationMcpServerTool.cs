using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginMutationMcpServerTool<TRequest> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly RegisteredTool _tool;
    private readonly IMutationToolHandler<TRequest> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;

    public PluginMutationMcpServerTool(
        Tool protocolTool,
        RegisteredTool tool,
        IMutationToolHandler<TRequest> handler,
        IToolExecutionContextFactory contextFactory)
        : base(protocolTool)
    {
        _tool = tool;
        _handler = handler;
        _contextFactory = contextFactory;
    }

    protected override async ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = _contextFactory.CreateMutationContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        if (contextLease.Failure is not null)
        {
            return CreateResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Plugin mutation acquisition completed without a context.");
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
            return CreateResult(McpPublishedResultSerializer.SerializePluginFailure(failure), isError: true);
        }

        if (proposalResult.Outcome == PluginExecutionOutcome.NoChange || proposalResult.Data is null)
        {
            var noChange = PluginExecutionResult<MutationData>.NoChange(
                diagnostics: proposalResult.Diagnostics,
                warnings: proposalResult.Warnings);
            return CreateResult(McpPublishedResultSerializer.SerializePluginMutation(noChange), isError: false);
        }

        var stagedResult = await contextLease.StageAsync(
            _tool.Metadata.Name,
            proposalResult.Data,
            proposalResult.Diagnostics,
            proposalResult.Warnings,
            cancellationToken).ConfigureAwait(false);
        return CreateResult(
            McpPublishedResultSerializer.SerializePluginMutation(stagedResult),
            stagedResult.Outcome.IsError());
    }

    private static CallToolResult CreateResult(JsonElement content, bool isError)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = content,
            IsError = isError,
        };
    }
}
