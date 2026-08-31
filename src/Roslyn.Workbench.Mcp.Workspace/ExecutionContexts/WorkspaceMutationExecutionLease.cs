using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Owns the exclusive operation lease and stager associated with a mutation execution context.
/// </summary>
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

    /// <summary>
    /// Gets the execution context when one could be constructed.
    /// </summary>
    public IWorkspaceExecutionContext? Context { get; }

    /// <summary>
    /// Gets the transaction stager when mutation staging is available.
    /// </summary>
    public IWorkspaceMutationStager? Stager { get; }

    /// <summary>
    /// Gets the classified failure when context acquisition was rejected.
    /// </summary>
    public WorkspaceExecutionFailure? Failure { get; }

    /// <summary>
    /// Gets a value indicating whether context acquisition failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Failure))]
    [MemberNotNullWhen(false, nameof(Context), nameof(Stager))]
    public bool HasFailure => Failure is not null;

    /// <summary>
    /// Releases the underlying exclusive operation lease.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        _lease?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a successfully acquired mutation context lease.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="stager">The active-transaction stager.</param>
    /// <param name="lease">The optional underlying operation lease.</param>
    /// <returns>The acquired lease.</returns>
    public static WorkspaceMutationExecutionLease Acquired(
        IWorkspaceExecutionContext context,
        IWorkspaceMutationStager stager,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceMutationExecutionLease(context, stager, null, lease);
    }

    /// <summary>
    /// Creates a rejected mutation context lease.
    /// </summary>
    /// <param name="failure">The rejection.</param>
    /// <param name="context">The optional context available for response metadata.</param>
    /// <param name="stager">The optional stager available to the caller.</param>
    /// <param name="lease">The optional underlying operation lease.</param>
    /// <returns>The rejected lease.</returns>
    public static WorkspaceMutationExecutionLease Rejected(
        WorkspaceExecutionFailure failure,
        IWorkspaceExecutionContext? context = null,
        IWorkspaceMutationStager? stager = null,
        IWorkspaceOperationLease? lease = null)
    {
        return new WorkspaceMutationExecutionLease(context, stager, failure, lease);
    }
}
