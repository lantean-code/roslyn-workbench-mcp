using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ReplayCodeActionExecutor : IReplayCodeActionExecutor
{
    private readonly IToolResultShaper _resultShaper;

    public ReplayCodeActionExecutor(IToolResultShaper resultShaper)
    {
        _resultShaper = resultShaper;
    }

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
        if (selection is null)
        {
            return ValueTask.FromResult(_resultShaper.Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
        }

        return context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = selection,
            ExpectedSnapshot = expectedSnapshot,
            ProviderId = providerId,
            Title = title,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        }, cancellationToken);
    }
}
