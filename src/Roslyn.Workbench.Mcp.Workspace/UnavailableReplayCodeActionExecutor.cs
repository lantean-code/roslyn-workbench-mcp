using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableReplayCodeActionExecutor : IReplayCodeActionExecutor
{
    private const string _message = "Tool execution services are unavailable.";

    public ValueTask<PluginExecutionResult<MutationProposal>> StageReplaySelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        IMutationContext context,
        CancellationToken cancellationToken,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null)
    {
        _ = selection;
        _ = expectedSnapshot;
        _ = context;
        _ = cancellationToken;
        _ = providerId;
        _ = title;
        _ = titleStartsWith;
        _ = titleDoesNotContain;
        _ = equivalenceKey;
        _ = actionPath;

        return ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "ToolExecutionServicesUnavailable",
            Message = _message,
        }));
    }
}
