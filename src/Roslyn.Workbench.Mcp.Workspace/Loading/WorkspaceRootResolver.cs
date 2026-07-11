namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceRootResolver : IWorkspaceRootResolver
{
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly IFileSystem _fileSystem;

    public WorkspaceRootResolver(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string? Resolve(string loadedPath, string? requestedRoot)
    {
        if (string.IsNullOrWhiteSpace(loadedPath) || !_fileSystem.Path.IsPathFullyQualified(loadedPath))
        {
            return null;
        }

        var canonicalLoadedPath = _fileSystem.Path.GetFullPath(loadedPath);
        if (requestedRoot is not null)
        {
            if (string.IsNullOrWhiteSpace(requestedRoot) || !_fileSystem.Path.IsPathFullyQualified(requestedRoot))
            {
                return null;
            }

            var canonicalRequestedRoot = _fileSystem.Path.GetFullPath(requestedRoot);
            return _fileSystem.Directory.Exists(canonicalRequestedRoot)
                && Contains(canonicalRequestedRoot, canonicalLoadedPath)
                ? canonicalRequestedRoot
                : null;
        }

        var directory = _fileSystem.Path.GetDirectoryName(canonicalLoadedPath);
        var fallback = directory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (_fileSystem.Directory.Exists(_fileSystem.Path.Combine(directory, ".git"))
                || _fileSystem.File.Exists(_fileSystem.Path.Combine(directory, ".git")))
            {
                return directory;
            }

            var parent = _fileSystem.Path.GetDirectoryName(directory);
            if (string.Equals(parent, directory, PathComparison))
            {
                break;
            }

            directory = parent;
        }

        return fallback;
    }

    public bool Contains(string workspaceRoot, string path)
    {
        var relative = _fileSystem.Path.GetRelativePath(
            _fileSystem.Path.GetFullPath(workspaceRoot),
            _fileSystem.Path.GetFullPath(path));
        return relative != ".."
            && !relative.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", PathComparison)
            && !_fileSystem.Path.IsPathRooted(relative);
    }
}
