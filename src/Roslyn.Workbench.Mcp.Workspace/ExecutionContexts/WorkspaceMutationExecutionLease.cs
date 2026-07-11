namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceMutationExecutionLease : IAsyncDisposable
{
    private readonly IAsyncDisposable? _lease;

    private WorkspaceMutationExecutionLease(
        IWorkspaceExecutionContext? context,
        IWorkspaceMutationStager? stager,
        WorkspaceExecutionFailure? failure,
        IAsyncDisposable? lease)
    {
        Context = context;
        Stager = stager;
        Failure = failure;
        _lease = lease;
    }

    public IWorkspaceExecutionContext? Context { get; }

    public IWorkspaceMutationStager? Stager { get; }

    public WorkspaceExecutionFailure? Failure { get; }

    public static WorkspaceMutationExecutionLease Acquired(
        IWorkspaceExecutionContext context,
        IWorkspaceMutationStager stager,
        IAsyncDisposable? lease = null)
    {

        return new WorkspaceMutationExecutionLease(context, stager, null, lease);
    }

    public static WorkspaceMutationExecutionLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IWorkspaceMutationStager? stager = null,
        IAsyncDisposable? lease = null)
    {

        return new WorkspaceMutationExecutionLease(context, stager, failure, lease);
    }

    public ValueTask DisposeAsync()
    {
        return _lease is null ? ValueTask.CompletedTask : _lease.DisposeAsync();
    }
}
