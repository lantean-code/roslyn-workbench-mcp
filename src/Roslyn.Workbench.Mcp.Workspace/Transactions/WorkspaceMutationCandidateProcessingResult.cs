using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a normalized candidate solution or a structured processing error.
/// </summary>
internal sealed record WorkspaceMutationCandidateProcessingResult
{
    /// <summary>
    /// Gets the normalized candidate solution when processing succeeds.
    /// </summary>
    public Solution? Solution { get; }

    /// <summary>
    /// Gets the structured error when processing cannot produce a safe candidate.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets a value indicating whether processing produced a candidate solution.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Solution))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSucceeded => Solution is not null;

    private WorkspaceMutationCandidateProcessingResult(
        Solution? solution,
        WorkspaceOperationError? error)
    {
        Solution = solution;
        Error = error;
    }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    /// <param name="solution">The normalized candidate solution.</param>
    /// <returns>A result that represents successful completion.</returns>
    public static WorkspaceMutationCandidateProcessingResult Succeeded(Solution solution)
    {
        return new WorkspaceMutationCandidateProcessingResult(solution, error: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failure.</returns>
    public static WorkspaceMutationCandidateProcessingResult Failed(WorkspaceOperationError error)
    {
        return new WorkspaceMutationCandidateProcessingResult(solution: null, error);
    }
}
