using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Fallbacks;

internal sealed class UnavailableToolRequestResolver : IToolRequestResolver
{
    private const string _message = "Tool execution services are unavailable.";

    public ToolResolutionResult<Document, TResponse> ResolveDocument<TResponse>(DocumentSelector? selector, IToolExecutionContext context)
    {
        _ = selector;
        _ = context;

        return RejectResolution<Document, TResponse>();
    }

    public ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context)
    {
        _ = selector;
        _ = context;

        return RejectResolution<Project, TResponse>();
    }

    public ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        _ = scope;
        _ = context;

        return RejectResolution<IReadOnlyList<Document>, TResponse>();
    }

    public ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context)
    {
        _ = scope;
        _ = context;

        return RejectResolution<IReadOnlyList<Project>, TResponse>();
    }

    public ValueTask<ToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        IToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        _ = selector;
        _ = expectedSnapshot;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(RejectResolution<ISymbol, TResponse>());
    }

    public PluginExecutionResult<TResponse>? ValidateSnapshot<TResponse>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot)
    {
        _ = context;
        _ = expectedSnapshot;

        return Rejected<TResponse>();
    }

    private static PluginExecutionResult<TResponse> Rejected<TResponse>()
    {
        return PluginExecutionResult<TResponse>.Rejected(new ToolError
        {
            Code = "ToolExecutionServicesUnavailable",
            Message = _message,
        });
    }

    private static ToolResolutionResult<TValue, TResponse> RejectResolution<TValue, TResponse>()
        where TValue : class
    {
        return new ToolResolutionResult<TValue, TResponse>
        {
            Rejection = Rejected<TResponse>(),
        };
    }
}
