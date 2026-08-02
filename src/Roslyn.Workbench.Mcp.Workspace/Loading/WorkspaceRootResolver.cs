namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceRootResolver : IWorkspaceRootResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IPhysicalPathContainment _pathContainment;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    public WorkspaceRootResolver(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison,
        IPhysicalPathContainment pathContainment,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
        _pathContainment = pathContainment;
        _pathNormalizer = pathNormalizer;
    }

    public string? Resolve(string loadedPath, string? requestedRoot)
    {
        if (string.IsNullOrWhiteSpace(loadedPath)
            || !_fileSystem.Path.IsPathFullyQualified(loadedPath)
            || !_pathNormalizer.TryGetFullPath(loadedPath, out var canonicalLoadedPath))
        {
            return null;
        }

        if (requestedRoot is not null)
        {
            if (string.IsNullOrWhiteSpace(requestedRoot)
                || !_fileSystem.Path.IsPathFullyQualified(requestedRoot)
                || !_pathNormalizer.TryGetFullPath(requestedRoot, out var canonicalRequestedRoot))
            {
                return null;
            }

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
            if (string.Equals(parent, directory, _pathComparison.GetComparison(directory)))
            {
                break;
            }

            directory = parent;
        }

        return fallback;
    }

    public bool Contains(string workspaceRoot, string path)
    {
        return _pathContainment.TryGetContainedPath(workspaceRoot, path, out _);
    }
}
