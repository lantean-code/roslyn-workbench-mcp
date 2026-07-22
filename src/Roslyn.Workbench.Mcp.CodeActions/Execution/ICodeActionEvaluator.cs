namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionEvaluator
{
    ValueTask<CodeActionApplyResult> EvaluateAsync(
        CodeAction action,
        Solution solution,
        CancellationToken cancellationToken);
}
