namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed class WorkspacePathNormalizer : IWorkspacePathNormalizer
{
    private readonly IFileSystem _fileSystem;

    public WorkspacePathNormalizer(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool TryGetFullPath(string path, out string fullPath)
    {
        return TryNormalize(
            () => _fileSystem.Path.GetFullPath(path),
            out fullPath);
    }

    public bool TryGetFullPath(string path, string basePath, out string fullPath)
    {
        return TryNormalize(
            () => _fileSystem.Path.GetFullPath(path, basePath),
            out fullPath);
    }

    public bool TryGetWorkspaceRelativePath(string workspaceRoot, string path, out string relativePath)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            if (!TryGetFullPath(path, out var fullPath))
            {
                relativePath = string.Empty;
                return false;
            }

            relativePath = NormalizeSeparators(fullPath);
            return true;
        }

        if (!TryGetFullPath(workspaceRoot, out var canonicalRoot)
            || !TryGetFullPath(path, canonicalRoot, out var canonicalPath))
        {
            relativePath = string.Empty;
            return false;
        }

        return TryNormalize(
            () => NormalizeSeparators(_fileSystem.Path.GetRelativePath(canonicalRoot, canonicalPath)),
            out relativePath);
    }

    private static bool TryNormalize(Func<string> normalize, out string normalizedPath)
    {
        try
        {
            normalizedPath = normalize();
            return true;
        }
        catch (Exception exception) when (IsPathNormalizationFailure(exception))
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static bool IsPathNormalizationFailure(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('\\', '/');
    }
}
