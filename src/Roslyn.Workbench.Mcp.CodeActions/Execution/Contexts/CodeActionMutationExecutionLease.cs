using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

internal sealed class CodeActionMutationExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceMutationExecutionLease _workspaceLease;

    private CodeActionMutationExecutionLease(
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

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    public async ValueTask<CodeActionExecutionResult<MutationData>> StageAsync(
        string operationName,
        WorkspaceMutationCandidate candidate,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        if (_workspaceLease.HasFailure)
        {
            throw new InvalidOperationException("A rejected Code Action mutation lease cannot stage changes.");
        }

        var result = await _workspaceLease.Stager.StageAsync(
            operationName,
            candidate,
            diagnostics,
            warnings,
            cancellationToken);

        return CodeActionWorkspaceResultMapper.MapMutation(result);
    }

    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }

    public static CodeActionMutationExecutionLease Acquired(
        WorkspaceMutationExecutionLease workspaceLease,
        ICodeActionMutationContext context)
    {
        return new CodeActionMutationExecutionLease(workspaceLease, context, null);
    }

    public static CodeActionMutationExecutionLease Rejected(
        WorkspaceMutationExecutionLease workspaceLease,
        CodeActionExecutionFailure failure,
        ICodeActionMutationContext? context = null)
    {
        return new CodeActionMutationExecutionLease(workspaceLease, context, failure);
    }
}
