namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Evaluates Code Actions into candidate solutions while enforcing supported operation shapes.
/// </summary>
internal interface ICodeActionEvaluator
{
    /// <summary>
    /// Evaluates a Code Action and accepts only a single solution-changing operation.
    /// </summary>
    /// <param name="action">The Code Action to evaluate.</param>
    /// <param name="solution">The solution against which the action was resolved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The candidate solution or an unsupported-operation failure.</returns>
    ValueTask<CodeActionApplyResult> EvaluateAsync(
        CodeAction action,
        Solution solution,
        CancellationToken cancellationToken);
}
