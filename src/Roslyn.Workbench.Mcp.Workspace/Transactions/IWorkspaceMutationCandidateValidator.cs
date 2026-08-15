namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceMutationCandidateValidator
{
    WorkspaceOperationError? Validate(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot);
}
