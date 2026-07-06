namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution;

internal interface ICodeActionQueryWorkflow
{
    ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken);

    ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        IQueryContext context,
        CancellationToken cancellationToken);
}
