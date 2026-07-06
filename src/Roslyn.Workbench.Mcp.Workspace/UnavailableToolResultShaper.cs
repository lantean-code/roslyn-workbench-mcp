using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableToolResultShaper : IToolResultShaper
{
    private const string _message = "Tool execution services are unavailable.";

    public PluginExecutionResult<TResponse> EnsureWithinSize<TResponse>(IQueryContext context, TResponse data)
    {
        _ = context;
        _ = data;

        return Rejected<TResponse>();
    }

    public PluginExecutionResult<TResponse> RejectFromStatus<TResponse>(SelectorResolveStatus status, string targetName)
    {
        _ = status;
        _ = targetName;

        return Rejected<TResponse>();
    }

    public PluginExecutionResult<TResponse> Rejected<TResponse>(string code, string message, RequiredAction? requiredAction = null)
    {
        _ = code;
        _ = message;
        _ = requiredAction;

        return Rejected<TResponse>();
    }

    public PluginExecutionResult<TData> CreateBoundedCollectionResult<TData, TItem>(
        IQueryContext context,
        IReadOnlyList<TItem> orderedItems,
        int maxResults,
        Func<IReadOnlyList<TItem>, bool, TData> createData)
    {
        _ = context;
        _ = orderedItems;
        _ = maxResults;
        _ = createData;

        return Rejected<TData>();
    }

    private static PluginExecutionResult<TResponse> Rejected<TResponse>()
    {
        return PluginExecutionResult<TResponse>.Rejected(new ToolError
        {
            Code = "ToolExecutionServicesUnavailable",
            Message = _message,
        });
    }
}
