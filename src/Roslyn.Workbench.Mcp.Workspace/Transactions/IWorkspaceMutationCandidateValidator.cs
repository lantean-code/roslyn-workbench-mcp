namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface IWorkspaceMutationCandidateValidator
{
    WorkspaceMutationCandidateValidationResult Validate(
        Solution currentSolution,
        Solution candidateSolution,
        string workspaceRoot);
}
