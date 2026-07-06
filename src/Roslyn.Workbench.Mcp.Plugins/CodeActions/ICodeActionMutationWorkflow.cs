using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

/// <summary>
/// Provides host-owned code-action mutation operations available to mutation tool contexts.
/// </summary>
public interface ICodeActionMutationWorkflow
{
    /// <summary>
    /// Revalidates and stages a selected refactoring action.
    /// </summary>
    /// <param name="request">The stage request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one deterministic replayable refactoring and stages it directly.
    /// </summary>
    /// <param name="request">The replay request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and stages a selected code fix.
    /// </summary>
    /// <param name="request">The stage request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and stages a selected code fix across a broader scope.
    /// </summary>
    /// <param name="request">The fix-all request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one deterministic scoped code fix and stages its fix-all variant.
    /// </summary>
    /// <param name="request">The scoped code-fix request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one deterministic location-scoped code fix and stages it directly.
    /// </summary>
    /// <param name="request">The location-scoped code-fix request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        CancellationToken cancellationToken);
}
