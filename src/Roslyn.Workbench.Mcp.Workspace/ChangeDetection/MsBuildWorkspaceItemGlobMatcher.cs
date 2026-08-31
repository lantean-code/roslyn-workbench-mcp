using Microsoft.Build.Globbing;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Adapts an evaluated MSBuild glob to the Workspace item-membership contract.
/// </summary>
internal sealed class MsBuildWorkspaceItemGlobMatcher : IWorkspaceItemGlobMatcher
{
    private readonly IMSBuildGlob _glob;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsBuildWorkspaceItemGlobMatcher"/> class.
    /// </summary>
    /// <param name="glob">The evaluated MSBuild glob.</param>
    public MsBuildWorkspaceItemGlobMatcher(IMSBuildGlob glob)
    {
        _glob = glob;
    }

    /// <inheritdoc/>
    public bool Matches(string path)
    {
        return _glob.IsMatch(path);
    }
}
