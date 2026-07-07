using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceMutationContext : IMutationContext
{
    private readonly Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionMutationWorkflow _codeActionWorkflow;
    private readonly Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>> _stageAsync;

    public WorkspaceMutationContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspaceResolver resolver,
        Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionMutationWorkflow codeActionWorkflow,
        Func<RegisteredTool, MutationProposal, IReadOnlyList<DiagnosticInfo>, IReadOnlyList<WarningInfo>, CancellationToken, ValueTask<PluginExecutionResult<MutationData>>> stageAsync,
        IToolExecutionServices toolExecutionServices)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspaceResolver = resolver;
        _codeActionWorkflow = codeActionWorkflow;
        _stageAsync = stageAsync;
        ToolExecutionServices = toolExecutionServices;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public IToolExecutionServices ToolExecutionServices { get; }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageCodeActionAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageReplayCodeActionAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageFixAllAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageScopedCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _codeActionWorkflow.StageLocationCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<PluginExecutionResult<MutationData>> StageAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        return _stageAsync(tool, proposal, diagnostics, warnings, cancellationToken);
    }
}
