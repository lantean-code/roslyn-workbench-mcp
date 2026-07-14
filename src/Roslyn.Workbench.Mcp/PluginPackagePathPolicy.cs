using System.IO.Abstractions;

namespace Roslyn.Workbench.Mcp;

internal sealed class PluginPackagePathPolicy : IPluginPackagePathPolicy
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;

    public StringComparer Comparer => _pathComparison.Comparer;

    public PluginPackagePathPolicy(
        IFileSystem fileSystem,
        IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    public bool TryGetContainedPath(string packageDirectory, string candidatePath, out string containedPath)
    {
        try
        {
            var canonicalPackageDirectory = _fileSystem.Path.GetFullPath(packageDirectory);
            var canonicalCandidatePath = _fileSystem.Path.GetFullPath(candidatePath);
            if (!IsContained(canonicalPackageDirectory, canonicalCandidatePath))
            {
                containedPath = string.Empty;
                return false;
            }

            var resolvedPackageDirectory = ResolveDirectoryLink(canonicalPackageDirectory);
            if (!IsResolvedPathContained(canonicalPackageDirectory, resolvedPackageDirectory, canonicalCandidatePath))
            {
                containedPath = string.Empty;
                return false;
            }

            containedPath = canonicalCandidatePath;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            containedPath = string.Empty;
            return false;
        }
    }

    private bool IsResolvedPathContained(
        string canonicalPackageDirectory,
        string resolvedPackageDirectory,
        string canonicalCandidatePath)
    {
        var relativePath = _fileSystem.Path.GetRelativePath(canonicalPackageDirectory, canonicalCandidatePath);
        var resolvedPath = resolvedPackageDirectory;
        foreach (var segment in SplitPath(relativePath))
        {
            resolvedPath = _fileSystem.Path.Combine(resolvedPath, segment);
            resolvedPath = ResolveExistingLink(resolvedPath);
            if (!IsContained(resolvedPackageDirectory, resolvedPath))
            {
                return false;
            }
        }

        return true;
    }

    private string ResolveExistingLink(string path)
    {
        IFileSystemInfo? linkTarget = null;
        if (_fileSystem.Directory.Exists(path))
        {
            linkTarget = _fileSystem.Directory.ResolveLinkTarget(path, true);
        }
        else if (_fileSystem.File.Exists(path))
        {
            linkTarget = _fileSystem.File.ResolveLinkTarget(path, true);
        }

        return _fileSystem.Path.GetFullPath(linkTarget?.FullName ?? path);
    }

    private string ResolveDirectoryLink(string path)
    {
        var linkTarget = _fileSystem.Directory.Exists(path)
            ? _fileSystem.Directory.ResolveLinkTarget(path, true)
            : null;
        return _fileSystem.Path.GetFullPath(linkTarget?.FullName ?? path);
    }

    private bool IsContained(string directory, string path)
    {
        var relativePath = _fileSystem.Path.GetRelativePath(directory, path);
        return !_fileSystem.Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", _pathComparison.Comparison)
            && !relativePath.StartsWith($"..{_fileSystem.Path.DirectorySeparatorChar}", _pathComparison.Comparison)
            && !relativePath.StartsWith($"..{_fileSystem.Path.AltDirectorySeparatorChar}", _pathComparison.Comparison);
    }

    private IEnumerable<string> SplitPath(string relativePath)
    {
        return relativePath.Split(
            [_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
    }
}
