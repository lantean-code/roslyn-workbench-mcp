using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Represents either successful release of process-wide transaction ownership or an ownership invariant failure.
/// </summary>
internal sealed record TransactionCompletionResult
{
    private static readonly TransactionCompletionResult _completed = new(isCompleted: true, failure: null);

    /// <summary>
    /// Gets whether transaction ownership was released successfully.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsCompleted { get; }

    /// <summary>
    /// Gets the ownership failure when completion was not safe.
    /// </summary>
    public TransactionCompletionFailure? Failure { get; }

    private TransactionCompletionResult(bool isCompleted, TransactionCompletionFailure? failure)
    {
        IsCompleted = isCompleted;
        Failure = failure;
    }

    /// <summary>
    /// Returns the shared successful completion result.
    /// </summary>
    /// <returns>A completed result.</returns>
    public static TransactionCompletionResult Completed()
    {
        return _completed;
    }

    /// <summary>
    /// Creates a failed completion for changed or lost transaction ownership.
    /// </summary>
    /// <param name="expectedOwnerWorkspaceId">The Workspace expected to own the transaction.</param>
    /// <param name="observedOwnerWorkspaceId">The owner observed during completion.</param>
    /// <returns>A failed completion result.</returns>
    public static TransactionCompletionResult OwnershipChanged(
        Guid expectedOwnerWorkspaceId,
        Guid? observedOwnerWorkspaceId)
    {
        var failure = new TransactionCompletionFailure(
            expectedOwnerWorkspaceId,
            observedOwnerWorkspaceId);

        return new TransactionCompletionResult(isCompleted: false, failure);
    }
}
