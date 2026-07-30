using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionContextLease : IAsyncDisposable
{
    private readonly IWorkspaceOperationLease? _lease;

    private WorkspaceExecutionContextLease(
        IWorkspaceExecutionContext? context,
        WorkspaceExecutionFailure? failure,
        IWorkspaceOperationLease? lease)
    {
        Context = context;
        Failure = failure;
        _lease = lease;
    }

    public IWorkspaceExecutionContext? Context { get; }

    public WorkspaceExecutionFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    public static WorkspaceExecutionContextLease Acquired(
        IWorkspaceExecutionContext context,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceExecutionContextLease(context, null, lease);
    }

    public static WorkspaceExecutionContextLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceExecutionContextLease(context, failure, lease);
    }

    public ValueTask DisposeAsync()
    {
        _lease?.Dispose();
        return ValueTask.CompletedTask;
    }
}
