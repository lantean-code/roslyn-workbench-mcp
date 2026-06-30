using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides host-owned code-action discovery and staging services.
/// </summary>
public interface ICodeActionService
{
    /// <summary>
    /// Gets the current code-action component status.
    /// </summary>
    ComponentStatus Status { get; }

    /// <summary>
    /// Lists applicable code actions for the provided request.
    /// </summary>
    /// <param name="request">The action-list request.</param>
    /// <param name="context">The host-owned query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized list result.</returns>
    ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and describes one discovered code action.
    /// </summary>
    /// <param name="request">The describe request.</param>
    /// <param name="context">The host-owned query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized descriptor result.</returns>
    ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and stages a selected refactoring action.
    /// </summary>
    /// <param name="request">The stage request.</param>
    /// <param name="context">The host-owned mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one deterministic replayable refactoring and stages it directly.
    /// </summary>
    /// <param name="request">The replay request.</param>
    /// <param name="context">The host-owned mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and stages a selected code fix.
    /// </summary>
    /// <param name="request">The stage request.</param>
    /// <param name="context">The host-owned mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and stages a selected code fix across a broader scope.
    /// </summary>
    /// <param name="request">The fix-all request.</param>
    /// <param name="context">The host-owned mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one deterministic scoped code fix and stages its fix-all variant.
    /// </summary>
    /// <param name="request">The scoped code-fix request.</param>
    /// <param name="context">The host-owned mutation context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized mutation proposal result.</returns>
    ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
