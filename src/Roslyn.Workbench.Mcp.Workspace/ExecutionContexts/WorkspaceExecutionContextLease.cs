using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Owns the operation lease associated with an acquired or rejected query execution context.
/// </summary>
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

    /// <summary>
    /// Gets the execution context when one could be constructed.
    /// </summary>
    public IWorkspaceExecutionContext? Context { get; }

    /// <summary>
    /// Gets the classified failure when context acquisition was rejected.
    /// </summary>
    public WorkspaceExecutionFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether context acquisition failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context))]
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Creates a successfully acquired query context lease.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="lease">The optional underlying operation lease.</param>
    /// <returns>The acquired lease.</returns>
    public static WorkspaceExecutionContextLease Acquired(
        IWorkspaceExecutionContext context,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceExecutionContextLease(context, null, lease);
    }

    /// <summary>
    /// Creates a rejected query context lease.
    /// </summary>
    /// <param name="failure">The rejection.</param>
    /// <param name="context">The optional context available for response metadata.</param>
    /// <param name="lease">The optional underlying operation lease.</param>
    /// <returns>The rejected lease.</returns>
    public static WorkspaceExecutionContextLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceExecutionContextLease(context, failure, lease);
    }

    /// <summary>
    /// Releases the underlying operation lease.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _lease?.Dispose();
        return ValueTask.CompletedTask;
    }
}
