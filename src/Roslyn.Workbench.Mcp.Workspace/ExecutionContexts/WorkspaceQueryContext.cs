using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceQueryContext : ICodeActionQueryContext
{
    private readonly Roslyn.Workbench.Mcp.CodeActions.Execution.ICodeActionQueryWorkflow _codeActionWorkflow;

    public WorkspaceQueryContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspaceResolver resolver,
        Roslyn.Workbench.Mcp.CodeActions.Execution.ICodeActionQueryWorkflow codeActionWorkflow,
        IToolExecutionServices toolExecutionServices)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspaceResolver = resolver;
        _codeActionWorkflow = codeActionWorkflow;
        ToolExecutionServices = toolExecutionServices;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public IToolExecutionServices ToolExecutionServices { get; }

    public ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.ListCodeActionsAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.DescribeCodeActionAsync(request, this, cancellationToken);
    }
}
