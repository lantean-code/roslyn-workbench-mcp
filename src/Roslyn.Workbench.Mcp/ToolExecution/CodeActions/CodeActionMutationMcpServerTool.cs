using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionMutationMcpServerTool<TRequest> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly CodeActionToolMetadata _metadata;
    private readonly ICodeActionMutationToolHandler<TRequest> _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

    public CodeActionMutationMcpServerTool(
        Tool protocolTool,
        CodeActionToolMetadata metadata,
        ICodeActionMutationToolHandler<TRequest> handler,
        ICodeActionExecutionContextFactory contextFactory)
        : base(protocolTool)
    {
        _metadata = metadata;
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
                McpPublishedResultSerializer.SerializeCodeActionFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Code Action mutation acquisition completed without a context.");
        var proposalResult = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        if (proposalResult.Outcome.IsError())
        {
            var failure = new CodeActionExecutionFailure
            {
                Outcome = proposalResult.Outcome,
                Error = proposalResult.Error
                    ?? throw new InvalidOperationException("Code Action mutation failure must provide an error."),
                RequiredAction = proposalResult.RequiredAction,
            };
            return CreateResult(McpPublishedResultSerializer.SerializeCodeActionFailure(failure), isError: true);
        }

        if (proposalResult.Outcome == CodeActionExecutionOutcome.NoChange || proposalResult.Data is null)
        {
            var noChange = CodeActionExecutionResult<MutationData>.NoChange(
                diagnostics: proposalResult.Diagnostics,
                warnings: proposalResult.Warnings);
            return CreateResult(McpPublishedResultSerializer.SerializeCodeActionMutation(noChange), isError: false);
        }

        var stagedResult = await contextLease.StageAsync(
            _metadata.Name,
            proposalResult.Data,
            proposalResult.Diagnostics,
            proposalResult.Warnings,
            cancellationToken).ConfigureAwait(false);
        return CreateResult(
            McpPublishedResultSerializer.SerializeCodeActionMutation(stagedResult),
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
