namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class LoadedWorkspace : ILoadedWorkspace
{
    private readonly MSBuildWorkspace _workspace;
    private readonly Solution _initialSolution;

    public LoadedWorkspace(MSBuildWorkspace workspace)
    {
        _workspace = workspace;
        _initialSolution = workspace.CurrentSolution;
    }

    public Solution CurrentSolution => _workspace.CurrentSolution;

    public bool HasCurrentSolutionChanged => !ReferenceEquals(_initialSolution, _workspace.CurrentSolution);

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
