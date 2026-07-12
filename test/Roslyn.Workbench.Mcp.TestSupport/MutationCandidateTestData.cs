using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.TestSupport;

internal static class MutationCandidateTestData
{
    private static readonly AdhocWorkspace _workspace = new();

    internal static Solution Solution { get; } = _workspace.CurrentSolution;

    internal static WorkspaceMutationCandidate CreateWorkspaceCandidate()
    {
        return new WorkspaceMutationCandidate
        {
            CandidateSolution = Solution,
        };
    }

    internal static MutationCandidate CreatePluginCandidate()
    {
        return new MutationCandidate
        {
            CandidateSolution = Solution,
        };
    }
}
