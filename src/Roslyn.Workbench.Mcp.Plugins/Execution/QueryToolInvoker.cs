using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class QueryToolInvoker<TRequest, TResponse> : IPluginToolInvoker
    where TRequest : WorkspaceBoundRequest
{
    private readonly IQueryToolHandler<TRequest, TResponse> _handler;

    public QueryToolInvoker(IQueryToolHandler<TRequest, TResponse> handler)
    {
        _handler = handler;
    }

    public async ValueTask<PluginExecutionResultBox> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        var contextLease = contextFactory.CreateQueryContext(request, cancellationToken);
        await using var _ = contextLease.ConfigureAwait(false);

        if (contextLease.ShortCircuitResult is not null)
        {
            return contextLease.ShortCircuitResult;
        }

        var context = contextLease.Context
            ?? throw new InvalidOperationException("Query context acquisition completed without a query context.");
        var result = await _handler
            .ExecuteAsync(request, context, cancellationToken)
            .ConfigureAwait(false);

        return PluginExecutionResultBox.From(result);
    }
}
