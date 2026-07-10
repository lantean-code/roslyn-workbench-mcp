namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionQueryContext : ICodeActionQueryContext
{
    private readonly ICodeActionQueryWorkflow _workflow;

    public CodeActionQueryContext(
        IWorkspaceExecutionContext workspaceContext,
        ICodeActionQueryWorkflow workflow)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
        _workflow = workflow;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public ValueTask<CodeActionExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.ListCodeActionsAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.DescribeCodeActionAsync(request, this, cancellationToken);
    }
}
