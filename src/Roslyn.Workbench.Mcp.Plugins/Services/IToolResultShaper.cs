using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Shapes normalized plugin responses and rejections for tool execution.
/// </summary>
public interface IToolResultShaper
{
    /// <summary>
    /// Creates a singleton query response that respects the configured response-size limit.
    /// </summary>
    /// <typeparam name="TValue">The published value type.</typeparam>
    /// <param name="context">The current query context.</param>
    /// <param name="value">The published value.</param>
    /// <returns>A successful or rejected plugin result.</returns>
    PluginExecutionResult<QueryResponse<TValue>> CreateSingletonResponse<TValue>(IQueryContext context, TValue value);

    /// <summary>
    /// Ensures a singleton query payload fits within the configured response limit.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="context">The current query context.</param>
    /// <param name="data">The response payload.</param>
    /// <returns>A successful or rejected plugin result.</returns>
    PluginExecutionResult<TResponse> EnsureWithinSize<TResponse>(IQueryContext context, TResponse data);

    /// <summary>
    /// Creates a normalized rejection from a selector resolution status.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="status">The selector resolution status.</param>
    /// <param name="targetName">The resolved target display name.</param>
    /// <returns>The normalized rejection result.</returns>
    PluginExecutionResult<TResponse> RejectFromStatus<TResponse>(SelectorResolveStatus status, string targetName);

    /// <summary>
    /// Creates a normalized rejection result.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="requiredAction">The optional required follow-up action.</param>
    /// <returns>The normalized rejection result.</returns>
    PluginExecutionResult<TResponse> Rejected<TResponse>(string code, string message, RequiredAction? requiredAction = null);

    /// <summary>
    /// Creates a bounded collection result that respects result-count and response-size limits.
    /// </summary>
    /// <typeparam name="TData">The response payload type.</typeparam>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="context">The current query context.</param>
    /// <param name="orderedItems">The pre-ordered items.</param>
    /// <param name="maxResults">The maximum number of items to include.</param>
    /// <param name="createData">Creates the response payload from the selected items and truncation flag.</param>
    /// <returns>The bounded collection result.</returns>
    PluginExecutionResult<TData> CreateBoundedCollectionResult<TData, TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults,
        Func<IReadOnlyList<TItem>, bool, TData> createData);

    /// <summary>
    /// Creates a bounded collection response that respects result-count and response-size limits.
    /// </summary>
    /// <typeparam name="TItem">The collection item type.</typeparam>
    /// <param name="context">The current query context.</param>
    /// <param name="orderedItems">The pre-ordered items.</param>
    /// <param name="maxResults">The maximum number of items to include.</param>
    /// <returns>The bounded collection result.</returns>
    PluginExecutionResult<CollectionResponse<TItem>> CreateBoundedCollectionResponse<TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults);
}
