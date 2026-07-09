namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionQueryContext : IQueryContext
{
    ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken);
}
