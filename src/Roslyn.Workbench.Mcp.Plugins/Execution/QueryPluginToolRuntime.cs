using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins.Protocol;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class QueryPluginToolRuntime<TRequest, TResponse> : IPluginToolRuntime
    where TRequest : WorkspaceBoundRequest
{
    private readonly IQueryToolHandler<TRequest, TResponse> _handler;

    public QueryPluginToolRuntime(IQueryToolHandler<TRequest, TResponse> handler)
    {
        _handler = handler;
    }

    public async ValueTask<CallToolResult> InvokeAsync(
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        try
        {
            var request = ToolRequestBinder.Deserialize<TRequest>(arguments);
            var contextLease = contextFactory.CreateQueryContext(request, cancellationToken);
            await using var _ = contextLease.ConfigureAwait(false);

            if (contextLease.ShortCircuitResult is not null)
            {
                return CreateCallToolResult(ToolKind.Query, typeof(TResponse), contextLease.ShortCircuitResult);
            }

            var context = contextLease.Context
                ?? throw new InvalidOperationException("Query context acquisition completed without a query context.");
            var result = await _handler.ExecuteAsync(request, context, cancellationToken).ConfigureAwait(false);

            return CreateCallToolResult(ToolKind.Query, typeof(TResponse), PluginExecutionResultBox.From(result));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateCallToolResult(ToolKind.Query, typeof(TResponse), PluginExecutionResultBox.CreateUnhandledException());
        }
    }

    private static CallToolResult CreateCallToolResult(ToolKind kind, Type responseType, PluginExecutionResultBox result)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = PluginToolResultSerializer.Serialize(kind, responseType, result),
            IsError = result.Outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted,
        };
    }
}
