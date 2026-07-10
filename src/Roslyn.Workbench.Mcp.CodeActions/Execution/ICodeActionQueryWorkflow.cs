namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionQueryWorkflow
{
    ValueTask<CodeActionExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
