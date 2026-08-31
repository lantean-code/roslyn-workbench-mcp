namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Describes evaluated item membership beneath one search root outside the trusted Workspace boundary.
/// </summary>
internal sealed class WorkspaceExternalInputMembership
{
    private readonly FileSystemPathKey _searchRoot;
    private readonly string _searchRootPrefix;

    /// <summary>
    /// Gets the evaluated item globs that can select files beneath the search root.
    /// </summary>
    public IReadOnlyList<WorkspaceEvaluatedItemGlob> Globs { get; }

    /// <summary>
    /// Gets files already loaded from the search root, which remain relevant even when a later glob evaluation differs.
    /// </summary>
    public IReadOnlySet<FileSystemPathKey> LoadedPaths { get; }

    /// <summary>
    /// Gets the normalized external directory watched for membership changes.
    /// </summary>
    public string SearchRoot => _searchRoot.Path;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceExternalInputMembership"/> class.
    /// </summary>
    /// <param name="searchRoot">The normalized search root and its platform-aware comparison.</param>
    /// <param name="globs">The evaluated globs that can include files beneath the root.</param>
    /// <param name="loadedPaths">The files already loaded from the root.</param>
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

    /// <summary>
    /// Determines whether a path lies beneath the root and matches any evaluated item glob.
    /// </summary>
    /// <param name="path">The candidate absolute path.</param>
    /// <returns><see langword="true"/> when the path is a member; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a path is the search root or lies beneath it.
    /// </summary>
    /// <param name="path">The candidate absolute path.</param>
    /// <returns><see langword="true"/> when the root contains the path; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string path)
    {
        return string.Equals(path, SearchRoot, _searchRoot.Comparison)
            || path.StartsWith(_searchRootPrefix, _searchRoot.Comparison);
    }

    /// <summary>
    /// Determines whether a path contains any file already loaded from this external root.
    /// </summary>
    /// <param name="path">The candidate file or directory path.</param>
    /// <returns><see langword="true"/> when a loaded path is equal to or beneath the candidate; otherwise, <see langword="false"/>.</returns>
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
