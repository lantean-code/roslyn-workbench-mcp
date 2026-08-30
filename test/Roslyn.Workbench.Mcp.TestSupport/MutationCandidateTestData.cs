using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.TestSupport;

internal static class MutationCandidateTestData
{
    private static readonly AdhocWorkspace _workspace = new();

    public static Solution Solution { get; } = _workspace.CurrentSolution;

    public static WorkspaceMutationCandidate CreateWorkspaceCandidate()
    {
        return new WorkspaceMutationCandidate
        {
            CandidateSolution = Solution,
            Summary = "Summary",
        };
    }

    public static MutationCandidate CreatePluginCandidate()
    {
        return new MutationCandidate
        {
            CandidateSolution = Solution,
            Summary = "Summary",
        };
    }
}
