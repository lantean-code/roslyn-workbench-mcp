namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Derives path case sensitivity from the host platform and Linux mount metadata.
/// </summary>
internal sealed class WorkspacePathComparison : IWorkspacePathComparison
{
    private const string _mountInfoPath = "/proc/self/mountinfo";
    private readonly IFileSystem _fileSystem;
    private readonly Lazy<string[]> _mountInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacePathComparison"/> class.
    /// </summary>
    public WorkspacePathComparison()
        : this(new FileSystem())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacePathComparison"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system used to read Linux mount metadata.</param>
    public WorkspacePathComparison(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _mountInfo = new Lazy<string[]>(ReadMountInfo);
    }

    /// <inheritdoc/>
    public StringComparison GetComparison(string path)
    {
        return OperatingSystem.IsWindows() || IsCaseInsensitiveWindowsMount(path)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    /// <inheritdoc/>
    public FileSystemPathKey CreateKey(string path)
    {
        var isCaseSensitive = GetComparison(path) == StringComparison.Ordinal;
        return new FileSystemPathKey(path, isCaseSensitive);
    }

    /// <inheritdoc/>
    public bool IsWindowsFileSystemPath(string path)
    {
        return OperatingSystem.IsLinux()
            && TryGetWindowsMount(path, out _);
    }

    private bool IsCaseInsensitiveWindowsMount(string path)
    {
        return OperatingSystem.IsLinux()
            && TryGetWindowsMount(path, out var isCaseSensitive)
            && !isCaseSensitive;
    }

    private bool TryGetWindowsMount(string path, out bool isCaseSensitive)
    {
        isCaseSensitive = false;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var canonicalPath = _fileSystem.Path.GetFullPath(path);
        var matchedMountLength = -1;
        var matchedMountIsWindows = false;
        foreach (var line in _mountInfo.Value)
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var separatorIndex = Array.IndexOf(fields, "-");
            if (separatorIndex < 6 || separatorIndex + 3 >= fields.Length)
            {
                continue;
            }

            var mountPoint = UnescapeMountInfoPath(fields[4]);
            if (mountPoint.Length <= matchedMountLength || !ContainsPath(mountPoint, canonicalPath))
            {
                continue;
            }

            matchedMountLength = mountPoint.Length;
            var fileSystemType = fields[separatorIndex + 1];
            var mountOptions = fields[5];
            var superOptions = fields[separatorIndex + 3];
            var isWindowsMount = string.Equals(fileSystemType, "drvfs", StringComparison.Ordinal)
                || superOptions.Split(',').Any(static option => option.StartsWith("aname=drvfs", StringComparison.Ordinal));

            var hasCaseSensitiveOption = mountOptions.Split(',').Any(IsCaseSensitiveOption)
                || superOptions.Split(',').Any(IsCaseSensitiveOption);

            matchedMountIsWindows = isWindowsMount;
            isCaseSensitive = hasCaseSensitiveOption;
        }

        return matchedMountIsWindows;
    }

    private string[] ReadMountInfo()
    {
        if (!_fileSystem.File.Exists(_mountInfoPath))
        {
            return [];
        }

        try
        {
            return _fileSystem.File.ReadAllLines(_mountInfoPath);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool ContainsPath(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedPath = Path.GetFullPath(path);
        if (string.Equals(normalizedRoot, normalizedPath, StringComparison.Ordinal))
        {
            return true;
        }

        var rootPrefix = Path.EndsInDirectorySeparator(normalizedRoot)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(rootPrefix, StringComparison.Ordinal);
    }

    private static bool IsCaseSensitiveOption(string option)
    {
        return string.Equals(option, "case=dir", StringComparison.Ordinal)
            || string.Equals(option, "case=force", StringComparison.Ordinal);
    }

    private static string UnescapeMountInfoPath(string path)
    {
        return path.Replace("\\040", " ", StringComparison.Ordinal)
            .Replace("\\011", "\t", StringComparison.Ordinal)
            .Replace("\\012", "\n", StringComparison.Ordinal)
            .Replace("\\134", "\\", StringComparison.Ordinal);
    }
}
