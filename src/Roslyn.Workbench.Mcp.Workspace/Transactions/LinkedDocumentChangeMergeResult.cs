using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record LinkedDocumentChangeMergeResult
{
    public Solution? Solution { get; }

    public WorkspaceOperationError? Error { get; }

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

    public static LinkedDocumentChangeMergeResult Succeeded(Solution solution)
    {
        return new LinkedDocumentChangeMergeResult(solution, error: null);
    }

    public static LinkedDocumentChangeMergeResult Failed(WorkspaceOperationError error)
    {
        return new LinkedDocumentChangeMergeResult(solution: null, error);
    }
}
