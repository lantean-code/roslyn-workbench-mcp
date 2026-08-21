using Microsoft.Build.Globbing;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class MsBuildWorkspaceItemGlobMatcher : IWorkspaceItemGlobMatcher
{
    private readonly IMSBuildGlob _glob;

    public MsBuildWorkspaceItemGlobMatcher(IMSBuildGlob glob)
    {
        _glob = glob;
    }

    public bool Matches(string path)
    {
        return _glob.IsMatch(path);
    }
}
