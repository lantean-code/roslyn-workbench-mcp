namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionQueryExecutionLease : IAsyncDisposable
{
    private readonly WorkspaceExecutionContextLease _workspaceLease;

    public CodeActionQueryExecutionLease(
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

    public ValueTask DisposeAsync()
    {
        return _workspaceLease.DisposeAsync();
    }
}
