using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultToolResultShaper : IToolResultShaper
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public PluginExecutionResult<QueryResponse<TValue>> CreateSingletonResponse<TValue>(IQueryContext context, TValue value)
    {
        var response = new QueryResponse<TValue>
        {
            Value = value,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, _serializerOptions);
        return bytes.Length > context.MaxResponseBytes
            ? Rejected<QueryResponse<TValue>>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest)
            : PluginExecutionResult<QueryResponse<TValue>>.Success(response);
    }

    public PluginExecutionResult<TResponse> EnsureWithinSize<TResponse>(IQueryContext context, TResponse data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);
        return bytes.Length > context.MaxResponseBytes
            ? Rejected<TResponse>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest)
            : PluginExecutionResult<TResponse>.Success(data);
    }

    public PluginExecutionResult<TResponse> RejectFromStatus<TResponse>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<TResponse>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<TResponse>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public PluginExecutionResult<TResponse> Rejected<TResponse>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<TResponse>.Rejected(new ToolError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    public PluginExecutionResult<CollectionResponse<TItem>> CreateBoundedCollectionResponse<TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(orderedItems);

        var limitedCount = Math.Min(maxResults, orderedItems.Count);

        for (var count = limitedCount; count >= 0; count--)
        {
            var items = count == orderedItems.Count ? orderedItems : orderedItems.Take(count).ToArray();
            var response = new CollectionResponse<TItem>
            {
                Items = items,
                HasMore = count < orderedItems.Count,
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(response, _serializerOptions);

            if (bytes.Length <= context.MaxResponseBytes)
            {
                return PluginExecutionResult<CollectionResponse<TItem>>.Success(response);
            }
        }

        return Rejected<CollectionResponse<TItem>>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest);
    }

    public PluginExecutionResult<TData> CreateBoundedCollectionResult<TData, TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults,
        Func<IReadOnlyList<TItem>, bool, TData> createData)
    {
        ArgumentNullException.ThrowIfNull(createData);

        var limitedCount = Math.Min(maxResults, orderedItems.Count);

        for (var count = limitedCount; count >= 0; count--)
        {
            var items = count == orderedItems.Count ? orderedItems : orderedItems.Take(count).ToArray();
            var hasMore = count < orderedItems.Count;
            var data = createData(items, hasMore);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);

            if (bytes.Length <= context.MaxResponseBytes)
            {
                return PluginExecutionResult<TData>.Success(data);
            }
        }

        return Rejected<TData>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest);
    }
}
