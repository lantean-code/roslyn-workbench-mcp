namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionQueryWorkflow
{
    ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken);
}
