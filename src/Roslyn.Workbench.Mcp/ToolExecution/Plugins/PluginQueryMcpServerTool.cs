using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution.Plugins;

internal sealed class PluginQueryMcpServerTool<TRequest, TResponse> : McpServerToolBase
    where TRequest : WorkspaceBoundRequest
{
    private readonly IQueryToolHandler<TRequest, TResponse> _handler;
    private readonly IToolExecutionContextFactory _contextFactory;

    public PluginQueryMcpServerTool(
        Tool protocolTool,
        IQueryToolHandler<TRequest, TResponse> handler,
        IToolExecutionContextFactory contextFactory)
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
        if (contextLease.ShortCircuitResult is not null)
        {
            return CreateResult(
                McpPublishedResultSerializer.SerializePluginFailure(contextLease.ShortCircuitResult),
                isError: true);
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Plugin query acquisition completed without a context.");
        var result = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);
        return CreateResult(
            McpPublishedResultSerializer.SerializePluginQuery(result),
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
