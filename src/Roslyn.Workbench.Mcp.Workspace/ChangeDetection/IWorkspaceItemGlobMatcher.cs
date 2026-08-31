namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Determines whether a path belongs to an evaluated MSBuild item glob.
/// </summary>
internal interface IWorkspaceItemGlobMatcher
{
    /// <summary>
    /// Determines whether the supplied path is included after applying the glob's excludes and removals.
    /// </summary>
    /// <param name="path">The candidate absolute path.</param>
    /// <returns><see langword="true"/> when the path belongs to the evaluated item set; otherwise, <see langword="false"/>.</returns>
    bool Matches(string path);
}
