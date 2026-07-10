using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

internal sealed class CodeActionQueryMcpServerTool<TRequest, TResponse> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly ICodeActionQueryToolHandler<TRequest, TResponse> _handler;
    private readonly ICodeActionExecutionContextFactory _contextFactory;

    public CodeActionQueryMcpServerTool(
        Tool protocolTool,
        ICodeActionQueryToolHandler<TRequest, TResponse> handler,
        ICodeActionExecutionContextFactory contextFactory)
        : base(protocolTool)
    {
        _handler = handler;
        _contextFactory = contextFactory;
    }

    protected override async ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = _contextFactory.CreateQueryContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);
        if (contextLease.Failure is not null)
        {
            return CreateResult(
                McpPublishedResultSerializer.SerializeCodeActionFailure(contextLease.Failure),
                isError: true);
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Code Action query acquisition completed without a context.");
        var result = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        return CreateResult(
            McpPublishedResultSerializer.SerializeCodeActionQuery(result),
            result.Outcome.IsError());
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
