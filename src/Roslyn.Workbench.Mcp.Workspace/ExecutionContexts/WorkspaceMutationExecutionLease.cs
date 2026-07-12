namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceMutationExecutionLease : IAsyncDisposable
{
    private readonly IWorkspaceOperationLease? _lease;

    private WorkspaceMutationExecutionLease(
        IWorkspaceExecutionContext? context,
        IWorkspaceMutationStager? stager,
        WorkspaceExecutionFailure? failure,
        IWorkspaceOperationLease? lease)
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
        IWorkspaceOperationLease? lease = null)
    {

        return new WorkspaceMutationExecutionLease(context, stager, null, lease);
    }

    public static WorkspaceMutationExecutionLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IWorkspaceMutationStager? stager = null,
        IWorkspaceOperationLease? lease = null)
    {

        return new WorkspaceMutationExecutionLease(context, stager, failure, lease);
    }

    public ValueTask DisposeAsync()
    {
        _lease?.Dispose();
        return ValueTask.CompletedTask;
    }
}
