namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface ILoadedWorkspace : IDisposable
{
    Solution CurrentSolution { get; }

    void ApplyChanges(Solution solution);
}
