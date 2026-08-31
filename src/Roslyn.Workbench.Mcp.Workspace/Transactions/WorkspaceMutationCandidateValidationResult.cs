using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a valid mutation candidate or a structured validation error.
/// </summary>
internal sealed class WorkspaceMutationCandidateValidationResult
{
    /// <summary>
    /// Gets the structured error when validation fails.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets a value indicating whether the candidate is safe to stage.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Error is null;

    private WorkspaceMutationCandidateValidationResult(WorkspaceOperationError? error)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a result that represents successful validation.
    /// </summary>
    /// <returns>A result that represents successful validation.</returns>
    public static WorkspaceMutationCandidateValidationResult Valid()
    {
        return new WorkspaceMutationCandidateValidationResult(error: null);
    }

    /// <summary>
    /// Creates a result that represents failed validation.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failed validation.</returns>
    public static WorkspaceMutationCandidateValidationResult Invalid(WorkspaceOperationError error)
    {
        return new WorkspaceMutationCandidateValidationResult(error);
    }
}
