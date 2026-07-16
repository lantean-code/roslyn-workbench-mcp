using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionQueryExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceExecutionContextLease _workspaceLease;

    private CodeActionQueryExecutionLease(
        WorkspaceExecutionContextLease workspaceLease,
        ICodeActionQueryContext? context,
        CodeActionExecutionFailure? failure)
    {
        _workspaceLease = workspaceLease;
        Context = context;
        Failure = failure;
    }

    public ICodeActionQueryContext? Context { get; }

    public CodeActionExecutionFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }

    public static CodeActionQueryExecutionLease Acquired(
        WorkspaceExecutionContextLease workspaceLease,
        ICodeActionQueryContext context)
    {
        return new CodeActionQueryExecutionLease(workspaceLease, context, null);
    }

    public static CodeActionQueryExecutionLease Rejected(
        WorkspaceExecutionContextLease workspaceLease,
        CodeActionExecutionFailure failure,
        ICodeActionQueryContext? context = null)
    {
        return new CodeActionQueryExecutionLease(workspaceLease, context, failure);
    }
}
