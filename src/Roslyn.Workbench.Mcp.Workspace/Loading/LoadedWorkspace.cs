namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class LoadedWorkspace : ILoadedWorkspace
{
    private readonly MSBuildWorkspace _workspace;

    public LoadedWorkspace(MSBuildWorkspace workspace)
    {
        _workspace = workspace;
    }

    public Solution CurrentSolution => _workspace.CurrentSolution;

    public void ApplyChanges(Solution solution)
    {
        try
        {
            _ = _workspace.TryApplyChanges(solution);
        }
        catch (NotSupportedException)
        {
        }
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
