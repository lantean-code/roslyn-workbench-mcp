namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionMutationExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceMutationExecutionLease _workspaceLease;

    public CodeActionMutationExecutionLease(
        WorkspaceMutationExecutionLease workspaceLease,
        ICodeActionMutationContext? context,
        CodeActionExecutionFailure? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    public ICodeActionMutationContext? Context { get; }

    public CodeActionExecutionFailure? Failure { get; }

    public async ValueTask<CodeActionExecutionResult<MutationData>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        var stager = _workspaceLease.Stager
            ?? throw new InvalidOperationException("Code Action mutation acquisition completed without a mutation stager.");
        var result = await stager.StageAsync(
            operationName,
            candidate,
            diagnostics,
            warnings,
            cancellationToken).ConfigureAwait(false);

        return CodeActionWorkspaceResultMapper.MapMutation(result);
    }

    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }
}
