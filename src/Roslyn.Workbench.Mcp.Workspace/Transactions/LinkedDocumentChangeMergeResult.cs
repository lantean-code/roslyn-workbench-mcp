using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a candidate solution with reconciled linked-document edits or a merge error.
/// </summary>
internal sealed record LinkedDocumentChangeMergeResult
{
    /// <summary>
    /// Gets the reconciled candidate solution when merging succeeds.
    /// </summary>
    public Solution? Solution { get; }

    /// <summary>
    /// Gets the structured conflict when linked edits cannot be reconciled.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets a value indicating whether linked-document changes were merged successfully.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Solution))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSucceeded => Solution is not null;

    private LinkedDocumentChangeMergeResult(
        Solution? solution,
        WorkspaceOperationError? error)
    {
        Solution = solution;
        Error = error;
    }

    /// <summary>
    /// Creates a successful linked-document merge.
    /// </summary>
    /// <param name="solution">The candidate solution containing reconciled linked edits.</param>
    /// <returns>A result containing the reconciled solution.</returns>
    public static LinkedDocumentChangeMergeResult Succeeded(Solution solution)
    {
        return new LinkedDocumentChangeMergeResult(solution, error: null);
    }

    /// <summary>
    /// Creates a failed linked-document merge.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result containing the merge conflict.</returns>
    public static LinkedDocumentChangeMergeResult Failed(WorkspaceOperationError error)
    {
        return new LinkedDocumentChangeMergeResult(solution: null, error);
    }
}
