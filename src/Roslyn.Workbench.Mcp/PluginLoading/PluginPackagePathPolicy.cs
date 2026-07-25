namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginPackagePathPolicy : IPluginPackagePathPolicy
{
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IPhysicalPathContainment _pathContainment;

    public StringComparer Comparer => _pathComparison.Comparer;

    public PluginPackagePathPolicy(
        IWorkspacePathComparison pathComparison,
        IPhysicalPathContainment pathContainment)
    {
        _pathComparison = pathComparison;
        _pathContainment = pathContainment;
    }

    public bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath)
    {
        return _pathContainment.TryGetContainedPath(
            packageDirectory,
            candidatePath,
            out containedPath);
    }
}
