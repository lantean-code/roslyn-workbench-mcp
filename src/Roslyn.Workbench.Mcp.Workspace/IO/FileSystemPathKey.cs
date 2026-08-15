namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal readonly struct FileSystemPathKey : IEquatable<FileSystemPathKey>
{
    private readonly bool _isCaseSensitive;
    private readonly string? _path;

    public string Path => _path ?? string.Empty;

    public StringComparison Comparison => _isCaseSensitive
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;

    public FileSystemPathKey(string path, bool isCaseSensitive)
    {
        _path = path;
        _isCaseSensitive = isCaseSensitive;
    }

    public bool Equals(FileSystemPathKey other)
    {
        return _isCaseSensitive == other._isCaseSensitive
            && string.Equals(Path, other.Path, Comparison);
    }

    public override bool Equals(object? obj)
    {
        return obj is FileSystemPathKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        var pathHashCode = _isCaseSensitive
            ? StringComparer.Ordinal.GetHashCode(Path)
            : StringComparer.OrdinalIgnoreCase.GetHashCode(Path);

        return HashCode.Combine(_isCaseSensitive, pathHashCode);
    }

    public static bool operator ==(FileSystemPathKey left, FileSystemPathKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FileSystemPathKey left, FileSystemPathKey right)
    {
        return !left.Equals(right);
    }
}
