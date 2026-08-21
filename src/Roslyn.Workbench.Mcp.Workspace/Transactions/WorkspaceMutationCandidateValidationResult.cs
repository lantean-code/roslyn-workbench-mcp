using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceMutationCandidateValidationResult
{
    public WorkspaceOperationError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Error is null;

    private WorkspaceMutationCandidateValidationResult(WorkspaceOperationError? error)
    {
        Error = error;
    }

    public static WorkspaceMutationCandidateValidationResult Valid()
    {
        return new WorkspaceMutationCandidateValidationResult(error: null);
    }

    public static WorkspaceMutationCandidateValidationResult Invalid(WorkspaceOperationError error)
    {
        return new WorkspaceMutationCandidateValidationResult(error);
    }
}
