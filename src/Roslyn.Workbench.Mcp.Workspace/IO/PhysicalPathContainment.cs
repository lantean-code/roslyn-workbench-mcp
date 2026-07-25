using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed class PhysicalPathContainment : IPhysicalPathContainment
{
    private readonly IFileSystem _fileSystem;
    private readonly IWorkspacePathComparison _pathComparison;

    public PhysicalPathContainment(IFileSystem fileSystem, IWorkspacePathComparison pathComparison)
    {
        _fileSystem = fileSystem;
        _pathComparison = pathComparison;
    }

    public bool TryGetContainedPath(string rootDirectory, string candidatePath, out string containedPath)
    {
        return TryGetContainedPath(rootDirectory, candidatePath, allowRoot: true, out containedPath);
    }

    public bool TryGetStrictlyContainedPath(string rootDirectory, string candidatePath, out string containedPath)
    {
        return TryGetContainedPath(rootDirectory, candidatePath, allowRoot: false, out containedPath);
    }

    private bool TryGetContainedPath(
        string rootDirectory,
        string candidatePath,
        bool allowRoot,
        out string containedPath)
    {
        try
        {
            var canonicalRoot = Path.TrimEndingDirectorySeparator(_fileSystem.Path.GetFullPath(rootDirectory));
            var canonicalCandidate = _fileSystem.Path.GetFullPath(candidatePath);
            if (!TryGetRelativePath(canonicalRoot, canonicalCandidate, allowRoot, out var relativePath))
            {
                containedPath = string.Empty;
                return false;
            }

            if (string.Equals(
                relativePath,
                ".",
                _pathComparison.GetComparison(canonicalRoot)))
            {
                containedPath = canonicalCandidate;
                return true;
            }

            var resolvedRoot = ResolveExistingLink(canonicalRoot);
            var resolvedPath = resolvedRoot;
            foreach (var segment in SplitPath(relativePath))
            {
                resolvedPath = _fileSystem.Path.Combine(resolvedPath, segment);
                resolvedPath = ResolveExistingLink(resolvedPath);
                if (!IsLexicallyContained(resolvedRoot, resolvedPath, allowRoot: true))
                {
                    containedPath = string.Empty;
                    return false;
                }
            }

            containedPath = canonicalCandidate;
            return true;
        }
        catch (Exception exception) when (IsPathResolutionFailure(exception))
        {
            containedPath = string.Empty;
            return false;
        }
    }

    private bool TryGetRelativePath(
        string canonicalRoot,
        string canonicalCandidate,
        bool allowRoot,
        [NotNullWhen(true)] out string? relativePath)
    {
        if (!IsLexicallyContained(canonicalRoot, canonicalCandidate, allowRoot))
        {
            relativePath = null;
            return false;
        }

        relativePath = _fileSystem.Path.GetRelativePath(canonicalRoot, canonicalCandidate);
        return true;
    }

    private string ResolveExistingLink(string path)
    {
        IFileSystemInfo? linkTarget = null;
        if (_fileSystem.Directory.Exists(path))
        {
            linkTarget = _fileSystem.Directory.ResolveLinkTarget(path, returnFinalTarget: true);
        }
        else if (_fileSystem.File.Exists(path))
        {
            linkTarget = _fileSystem.File.ResolveLinkTarget(path, returnFinalTarget: true);
        }

        return _fileSystem.Path.GetFullPath(linkTarget?.FullName ?? path);
    }

    private bool IsLexicallyContained(string rootDirectory, string candidatePath, bool allowRoot)
    {
        var relativePath = _fileSystem.Path.GetRelativePath(rootDirectory, candidatePath);
        var comparison = _pathComparison.GetComparison(rootDirectory);
        if (_fileSystem.Path.IsPathRooted(relativePath)
            || string.Equals(relativePath, "..", comparison)
            || relativePath.StartsWith(
                $"..{_fileSystem.Path.DirectorySeparatorChar}",
                comparison)
            || relativePath.StartsWith(
                $"..{_fileSystem.Path.AltDirectorySeparatorChar}",
                comparison))
        {
            return false;
        }

        return allowRoot || !string.Equals(relativePath, ".", comparison);
    }

    private string[] SplitPath(string relativePath)
    {
        return relativePath.Split(
            [_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsPathResolutionFailure(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;
    }
}
