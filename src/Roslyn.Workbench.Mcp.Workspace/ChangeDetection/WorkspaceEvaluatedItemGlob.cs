namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Couples an evaluated MSBuild item matcher with the directory roots whose changes may affect its membership.
/// </summary>
internal sealed class WorkspaceEvaluatedItemGlob
{
    private readonly IWorkspaceItemGlobMatcher _matcher;

    /// <summary>
    /// Gets the normalized directories from which the glob can include items.
    /// </summary>
    public IReadOnlyList<string> SearchRoots { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceEvaluatedItemGlob"/> class.
    /// </summary>
    /// <param name="matcher">The evaluated include, exclude and remove matcher.</param>
    /// <param name="searchRoots">The normalized directories searched by the glob.</param>
    public WorkspaceEvaluatedItemGlob(
        IWorkspaceItemGlobMatcher matcher,
        IReadOnlyList<string> searchRoots)
    {
        _matcher = matcher;
        SearchRoots = searchRoots;
    }

    /// <summary>
    /// Determines whether an absolute path belongs to the evaluated item set.
    /// </summary>
    /// <param name="path">The candidate absolute path.</param>
    /// <returns><see langword="true"/> when the path matches; otherwise, <see langword="false"/>.</returns>
    public bool Matches(string path)
    {
        return _matcher.Matches(path);
    }
}
