namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionMutationContext : ICodeActionMutationContext
{
    private readonly ICodeActionMutationWorkflow _workflow;

    public CodeActionMutationContext(
        IWorkspaceExecutionContext workspaceContext,
        ICodeActionMutationWorkflow workflow)
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

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageCodeActionAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageReplayCodeActionAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageFixAllAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageScopedCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return _workflow.StageLocationCodeFixAsync(request, this, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageReplaySelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null)
    {
        if (selection is null)
        {
            return ValueTask.FromResult(ToolExecutionHelpers.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required."));
        }

        return StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = selection,
            ExpectedSnapshot = expectedSnapshot,
            ProviderId = providerId,
            Title = title,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        }, cancellationToken);
    }
}
