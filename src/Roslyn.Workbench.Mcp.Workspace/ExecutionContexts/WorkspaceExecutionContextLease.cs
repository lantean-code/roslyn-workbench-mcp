namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextLease : IAsyncDisposable
{
    private readonly IAsyncDisposable? _lease;

    private WorkspaceExecutionContextLease(
        IWorkspaceExecutionContext? context,
        WorkspaceExecutionFailure? failure,
        IAsyncDisposable? lease)
    {
        Context = context;
        Failure = failure;
        _lease = lease;
    }

    public IWorkspaceExecutionContext? Context { get; }

    public WorkspaceExecutionFailure? Failure { get; }

    public static WorkspaceExecutionContextLease Acquired(
        IWorkspaceExecutionContext context,
        IAsyncDisposable? lease = null)
    {

        return new WorkspaceExecutionContextLease(context, null, lease);
    }

    public static WorkspaceExecutionContextLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IAsyncDisposable? lease = null)
    {

        return new WorkspaceExecutionContextLease(context, failure, lease);
    }

    public ValueTask DisposeAsync()
    {
        return _lease is null ? ValueTask.CompletedTask : _lease.DisposeAsync();
    }
}
