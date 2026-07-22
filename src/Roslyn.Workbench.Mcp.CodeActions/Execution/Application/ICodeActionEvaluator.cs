namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

internal interface ICodeActionEvaluator
{
    ValueTask<CodeActionApplyResult> EvaluateAsync(
        CodeAction action,
        Solution solution,
        CancellationToken cancellationToken);
}
