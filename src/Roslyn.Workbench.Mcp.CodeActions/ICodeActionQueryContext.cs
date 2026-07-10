namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionQueryContext : ICodeActionExecutionContext
{
    ValueTask<CodeActionExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken);

    ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken);
}
