using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.CodeActions;

/// <summary>
/// Provides host-owned code-action query operations available to query tool contexts.
/// </summary>
public interface ICodeActionQueryWorkflow
{
    /// <summary>
    /// Lists applicable code actions for the provided request.
    /// </summary>
    /// <param name="request">The action-list request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized list result.</returns>
    ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates and describes one discovered code action.
    /// </summary>
    /// <param name="request">The describe request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The normalized descriptor result.</returns>
    ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken);
}
