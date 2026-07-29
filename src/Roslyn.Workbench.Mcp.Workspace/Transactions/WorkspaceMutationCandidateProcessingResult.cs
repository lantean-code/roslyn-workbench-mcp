using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationCandidateProcessingResult
{
    public Solution? Solution { get; }

    public WorkspaceOperationError? Error { get; }

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

    public static WorkspaceMutationCandidateProcessingResult Succeeded(Solution solution)
    {
        return new WorkspaceMutationCandidateProcessingResult(solution, error: null);
    }

    public static WorkspaceMutationCandidateProcessingResult Failed(WorkspaceOperationError error)
    {
        return new WorkspaceMutationCandidateProcessingResult(solution: null, error);
    }
}
