namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputPathPolicy
{
    public static WorkspaceInputPathPolicy TrackAll { get; } = new([], StringComparison.Ordinal);

    private readonly string[] _artifactRootPrefixes;
    private readonly StringComparison _comparison;

    public IReadOnlyList<string> ArtifactRoots { get; }

    private WorkspaceInputPathPolicy(
        IReadOnlyList<string> artifactRoots,
        StringComparison comparison)
    {
        ArtifactRoots = artifactRoots;
        _comparison = comparison;
        _artifactRootPrefixes = new string[artifactRoots.Count];
        for (var index = 0; index < artifactRoots.Count; index++)
        {
            var root = artifactRoots[index];
            _artifactRootPrefixes[index] = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
        }
    }

    public bool ShouldTrack(string? path)
    {
        if (!TryNormalizePath(path, out var normalizedPath))
        {
            return true;
        }

        for (var index = 0; index < ArtifactRoots.Count; index++)
        {
            if (string.Equals(normalizedPath, ArtifactRoots[index], _comparison)
                || normalizedPath.StartsWith(_artifactRootPrefixes[index], _comparison))
            {
                return false;
            }
        }

        return true;
    }

    public static WorkspaceInputPathPolicy Create(
        IEnumerable<string> artifactRoots,
        IEnumerable<string> protectedPaths,
        StringComparison comparison)
    {
        var comparer = comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var normalizedProtectedPaths = NormalizePaths(protectedPaths, comparer);
        var normalizedArtifactRoots = NormalizePaths(artifactRoots, comparer);
        var safeArtifactRoots = new List<string>(normalizedArtifactRoots.Count);
        foreach (var artifactRoot in normalizedArtifactRoots)
        {
            if (!ContainsAnyPath(artifactRoot, normalizedProtectedPaths, comparison))
            {
                safeArtifactRoots.Add(artifactRoot);
            }
        }

        var minimalArtifactRoots = RemoveNestedRoots(safeArtifactRoots, comparison);
        return new WorkspaceInputPathPolicy(minimalArtifactRoots, comparison);
    }

    private static bool ContainsAnyPath(
        string root,
        IReadOnlyList<string> paths,
        StringComparison comparison)
    {
        foreach (var path in paths)
        {
            if (ContainsPath(root, path, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> NormalizePaths(
        IEnumerable<string> paths,
        StringComparer comparer)
    {
        var normalizedPaths = new List<string>();
        var uniquePaths = new HashSet<string>(comparer);
        foreach (var path in paths)
        {
            if (!TryNormalizePath(path, out var normalizedPath)
                || !uniquePaths.Add(normalizedPath))
            {
                continue;
            }

            normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths;
    }

    private static List<string> RemoveNestedRoots(
        List<string> artifactRoots,
        StringComparison comparison)
    {
        artifactRoots.Sort(static (left, right) => left.Length.CompareTo(right.Length));
        var minimalRoots = new List<string>(artifactRoots.Count);
        foreach (var artifactRoot in artifactRoots)
        {
            var isNested = false;
            foreach (var existingRoot in minimalRoots)
            {
                if (ContainsPath(existingRoot, artifactRoot, comparison))
                {
                    isNested = true;
                    break;
                }
            }

            if (!isNested)
            {
                minimalRoots.Add(artifactRoot);
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
