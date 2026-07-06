namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Fallbacks;

internal sealed class UnavailableCodeActionQueryWorkflow : Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionQueryWorkflow
{
    public ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<CodeActionListData>());
    }

    public ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromResult(Rejected<DescribeCodeActionData>());
    }

    private static PluginExecutionResult<T> Rejected<T>()
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = "CodeActionsUnavailable",
            Message = "Code-action composition is unavailable.",
        });
    }
}
