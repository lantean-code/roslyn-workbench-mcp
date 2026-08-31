namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Provides case-aware equality for a normalized file-system path.
/// </summary>
internal readonly struct FileSystemPathKey : IEquatable<FileSystemPathKey>
{
    private readonly bool _isCaseSensitive;
    private readonly string? _path;

    /// <summary>
    /// Gets the normalized path represented by the key.
    /// </summary>
    public string Path => _path ?? string.Empty;

    /// <summary>
    /// Gets the ordinal comparison used for path equality.
    /// </summary>
    public StringComparison Comparison => _isCaseSensitive
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemPathKey"/> structure.
    /// </summary>
    /// <param name="path">The path of the file or directory being processed.</param>
    /// <param name="isCaseSensitive">Whether path-key equality distinguishes character casing.</param>
    public FileSystemPathKey(string path, bool isCaseSensitive)
    {
        _path = path;
        _isCaseSensitive = isCaseSensitive;
    }

    /// <summary>
    /// Determines whether this value equals the supplied value.
    /// </summary>
    /// <param name="other">The value to compare with this instance.</param>
    /// <returns><see langword="true"/> when both keys represent the same path under their comparison rules; otherwise, <see langword="false"/>.</returns>
    public bool Equals(FileSystemPathKey other)
    {
        return _isCaseSensitive == other._isCaseSensitive
            && string.Equals(Path, other.Path, Comparison);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is FileSystemPathKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var pathHashCode = _isCaseSensitive
            ? StringComparer.Ordinal.GetHashCode(Path)
            : StringComparer.OrdinalIgnoreCase.GetHashCode(Path);

        return HashCode.Combine(_isCaseSensitive, pathHashCode);
    }

    /// <summary>
    /// Determines whether two values are equal.
    /// </summary>
    /// <param name="left">The path key on the left side of the comparison.</param>
    /// <param name="right">The path key on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when both keys represent the same path under their comparison rules; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FileSystemPathKey left, FileSystemPathKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two values are unequal.
    /// </summary>
    /// <param name="left">The path key on the left side of the comparison.</param>
    /// <param name="right">The path key on the right side of the comparison.</param>
    /// <returns><see langword="true"/> when the keys represent different paths; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FileSystemPathKey left, FileSystemPathKey right)
    {
        return !left.Equals(right);
    }
}
