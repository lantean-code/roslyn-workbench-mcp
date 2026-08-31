namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Filters watcher events beneath safely excluded generated or artifact directories without hiding protected inputs.
/// </summary>
internal sealed class WorkspaceInputPathPolicy
{
    /// <summary>
    /// Gets a policy that monitors every valid path.
    /// </summary>
    public static WorkspaceInputPathPolicy MonitorAll { get; } = new([]);

    private readonly StringComparison[] _excludedDirectoryRootComparisons;
    private readonly string[] _excludedDirectoryRootPrefixes;

    /// <summary>
    /// Gets the minimal normalized directory roots excluded from monitoring.
    /// </summary>
    public IReadOnlyList<string> ExcludedDirectoryRoots { get; }

    private WorkspaceInputPathPolicy(
        IReadOnlyList<FileSystemPathKey> excludedDirectoryRoots)
    {
        ExcludedDirectoryRoots = excludedDirectoryRoots.Select(static key => key.Path).ToArray();
        _excludedDirectoryRootComparisons = new StringComparison[excludedDirectoryRoots.Count];
        _excludedDirectoryRootPrefixes = new string[excludedDirectoryRoots.Count];
        for (var index = 0; index < excludedDirectoryRoots.Count; index++)
        {
            var key = excludedDirectoryRoots[index];
            var root = key.Path;
            _excludedDirectoryRootComparisons[index] = key.Comparison;
            _excludedDirectoryRootPrefixes[index] = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
        }
    }

    /// <summary>
    /// Determines whether a watcher path is outside all excluded directory roots.
    /// </summary>
    /// <param name="path">The watcher path, which may be absent or malformed.</param>
    /// <returns><see langword="false"/> only when a valid normalized path lies within an excluded root.</returns>
    public bool ShouldMonitor(string? path)
    {
        if (!TryNormalizePath(path, out var normalizedPath))
        {
            return true;
        }

        for (var index = 0; index < ExcludedDirectoryRoots.Count; index++)
        {
            var comparison = _excludedDirectoryRootComparisons[index];
            if (string.Equals(normalizedPath, ExcludedDirectoryRoots[index], comparison)
                || normalizedPath.StartsWith(_excludedDirectoryRootPrefixes[index], comparison))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a policy that excludes only normalized roots which contain no protected Workspace input.
    /// </summary>
    /// <param name="excludedDirectoryRoots">Candidate generated or artifact directory roots.</param>
    /// <param name="protectedPaths">Inputs that must remain monitored even when beneath a candidate root.</param>
    /// <param name="pathComparison">The platform-aware path normalizer and comparer.</param>
    /// <returns>A policy containing the minimal safe exclusion roots.</returns>
    public static WorkspaceInputPathPolicy Create(
        IEnumerable<string> excludedDirectoryRoots,
        IEnumerable<string> protectedPaths,
        IWorkspacePathComparison pathComparison)
    {
        var normalizedProtectedPaths = NormalizePaths(protectedPaths, pathComparison);
        var normalizedExcludedDirectoryRoots = NormalizePaths(excludedDirectoryRoots, pathComparison);
        var safeExcludedDirectoryRoots = new List<FileSystemPathKey>(normalizedExcludedDirectoryRoots.Count);
        foreach (var excludedDirectoryRoot in normalizedExcludedDirectoryRoots)
        {
            if (!ContainsAnyPath(excludedDirectoryRoot, normalizedProtectedPaths))
            {
                safeExcludedDirectoryRoots.Add(excludedDirectoryRoot);
            }
        }

        var minimalExcludedDirectoryRoots = RemoveNestedRoots(safeExcludedDirectoryRoots);
        return new WorkspaceInputPathPolicy(minimalExcludedDirectoryRoots);
    }

    private static bool ContainsAnyPath(
        FileSystemPathKey root,
        IReadOnlyList<FileSystemPathKey> paths)
    {
        foreach (var path in paths)
        {
            if (ContainsPath(root.Path, path.Path, root.Comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static List<FileSystemPathKey> NormalizePaths(
        IEnumerable<string> paths,
        IWorkspacePathComparison pathComparison)
    {
        var normalizedPaths = new List<FileSystemPathKey>();
        var uniquePaths = new HashSet<FileSystemPathKey>();
        foreach (var path in paths)
        {
            if (!TryNormalizePath(path, out var normalizedPath))
            {
                continue;
            }

            var normalizedPathKey = pathComparison.CreateKey(normalizedPath);
            if (uniquePaths.Add(normalizedPathKey))
            {
                normalizedPaths.Add(normalizedPathKey);
            }
        }

        return normalizedPaths;
    }

    private static List<FileSystemPathKey> RemoveNestedRoots(List<FileSystemPathKey> excludedDirectoryRoots)
    {
        excludedDirectoryRoots.Sort(static (left, right) => left.Path.Length.CompareTo(right.Path.Length));
        var minimalRoots = new List<FileSystemPathKey>(excludedDirectoryRoots.Count);
        foreach (var excludedDirectoryRoot in excludedDirectoryRoots)
        {
            var isNested = false;
            foreach (var existingRoot in minimalRoots)
            {
                if (ContainsPath(existingRoot.Path, excludedDirectoryRoot.Path, existingRoot.Comparison))
                {
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                minimalRoots.Add(excludedDirectoryRoot);
            }
        }

        return minimalRoots;
    }

    private static bool ContainsPath(
        string root,
        string path,
        StringComparison comparison)
    {
        if (string.Equals(root, path, comparison))
        {
            return true;
        }

        var rootPrefix = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;

        return path.StartsWith(rootPrefix, comparison);
    }

    private static bool TryNormalizePath(
        string? path,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
