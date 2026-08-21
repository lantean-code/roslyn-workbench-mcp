namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceExternalInputMembership
{
    private readonly FileSystemPathKey _searchRoot;
    private readonly string _searchRootPrefix;

    public IReadOnlyList<WorkspaceEvaluatedItemGlob> Globs { get; }

    public IReadOnlySet<FileSystemPathKey> LoadedPaths { get; }

    public string SearchRoot => _searchRoot.Path;

    public WorkspaceExternalInputMembership(
        FileSystemPathKey searchRoot,
        IReadOnlyList<WorkspaceEvaluatedItemGlob> globs,
        IReadOnlySet<FileSystemPathKey> loadedPaths)
    {
        _searchRoot = searchRoot;
        _searchRootPrefix = Path.EndsInDirectorySeparator(searchRoot.Path)
            ? searchRoot.Path
            : searchRoot.Path + Path.DirectorySeparatorChar;

        Globs = globs;
        LoadedPaths = loadedPaths;
    }

    public bool Matches(string path)
    {
        if (!Contains(path))
        {
            return false;
        }

        foreach (var glob in Globs)
        {
            if (glob.Matches(path))
            {
                return true;
            }
        }

        return false;
    }

    public bool Contains(string path)
    {
        return string.Equals(path, SearchRoot, _searchRoot.Comparison)
            || path.StartsWith(_searchRootPrefix, _searchRoot.Comparison);
    }

    public bool ContainsLoadedPathWithin(string path)
    {
        var pathPrefix = Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;

        foreach (var loadedPath in LoadedPaths)
        {
            if (string.Equals(loadedPath.Path, path, _searchRoot.Comparison)
                || loadedPath.Path.StartsWith(pathPrefix, _searchRoot.Comparison))
            {
                return true;
            }
        }

        return false;
    }
}
