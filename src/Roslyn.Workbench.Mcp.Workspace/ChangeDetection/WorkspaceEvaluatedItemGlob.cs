namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceEvaluatedItemGlob
{
    private readonly IWorkspaceItemGlobMatcher _matcher;

    public IReadOnlyList<string> SearchRoots { get; }

    public WorkspaceEvaluatedItemGlob(
        IWorkspaceItemGlobMatcher matcher,
        IReadOnlyList<string> searchRoots)
    {
        _matcher = matcher;
        SearchRoots = searchRoots;
    }

    public bool Matches(string path)
    {
        return _matcher.Matches(path);
    }
}
