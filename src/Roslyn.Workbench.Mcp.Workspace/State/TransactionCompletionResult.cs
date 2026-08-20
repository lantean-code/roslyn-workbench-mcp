using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed record TransactionCompletionResult
{
    private static readonly TransactionCompletionResult _completed = new(isCompleted: true, failure: null);

    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsCompleted { get; }

    public TransactionCompletionFailure? Failure { get; }

    private TransactionCompletionResult(bool isCompleted, TransactionCompletionFailure? failure)
    {
        IsCompleted = isCompleted;
        Failure = failure;
    }

    public static TransactionCompletionResult Completed()
    {
        return _completed;
    }

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
