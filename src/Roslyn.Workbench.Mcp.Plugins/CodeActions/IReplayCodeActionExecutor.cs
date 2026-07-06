using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

/// <summary>
/// Revalidates and stages replayable Roslyn code actions.
/// </summary>
public interface IReplayCodeActionExecutor
{
    /// <summary>
    /// Revalidates and stages a replayable code action selection.
    /// </summary>
    /// <param name="selection">The selected source location.</param>
    /// <param name="expectedSnapshot">The expected snapshot precondition.</param>
    /// <param name="context">The current mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="providerId">The Roslyn provider identifier.</param>
    /// <param name="title">The exact expected code-action title.</param>
    /// <param name="titleStartsWith">The required title prefix.</param>
    /// <param name="titleDoesNotContain">The title text that must be absent.</param>
    /// <param name="equivalenceKey">The expected equivalence key.</param>
    /// <param name="actionPath">The deterministic action path.</param>
    /// <returns>The staged mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageReplaySelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        IMutationContext context,
        CancellationToken cancellationToken,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null);
}
