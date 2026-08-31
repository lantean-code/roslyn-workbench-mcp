using System.Runtime.Versioning;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Applies operating-system safeguards to durable Workspace state files and directories.
/// </summary>
internal sealed class WorkspaceStateDirectorySecurity : IWorkspaceStateDirectorySecurity
{
    private const string _writeProbePrefix = ".roslyn-workbench-write-probe";

    private const UnixFileMode _privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode _privateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceStateDirectorySecurity"/> class.
    /// </summary>
    /// <param name="fileSystem">The file-system abstraction used for storage operations.</param>
    public WorkspaceStateDirectorySecurity(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc/>
    public void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OperatingSystem.IsWindows())
        {
            _fileSystem.Directory.CreateDirectory(path);
        }
        else
        {
            _fileSystem.Directory.CreateDirectory(path, _privateDirectoryMode);
        }

        ValidateDirectory(path);
    }

    /// <inheritdoc/>
    public void ValidateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureNotRedirected(path, "directory");
        if (!OperatingSystem.IsWindows())
        {
            EnsureUnixMode(path, _privateDirectoryMode, "directory");
        }
    }

    /// <inheritdoc/>
    public void ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureNotRedirected(path, "file");
        if (!OperatingSystem.IsWindows())
        {
            EnsureUnixMode(path, _privateFileMode, "file");
        }
    }

    /// <inheritdoc/>
    public void ValidateWritableDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var probePath = _fileSystem.Path.Combine(path, $"{_writeProbePrefix}-{Guid.NewGuid():n}.tmp");
        try
        {
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Options = FileOptions.WriteThrough,
                Share = FileShare.None,
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = _privateFileMode;
            }

            using (var stream = _fileSystem.FileStream.New(probePath, options))
            {
                stream.WriteByte(0);
                stream.Flush(flushToDisk: true);
            }

            ValidateFile(probePath);
            _fileSystem.File.Delete(probePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteProbe(probePath);
            throw new InvalidOperationException(
                $"The Workspace recovery directory '{path}' is not writable. Configure --state-directory with a local directory that permits durable file creation and deletion.",
                exception);
        }
    }

    private void EnsureNotRedirected(string path, string kind)
    {
        var attributes = _fileSystem.File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The Workspace state {kind} '{path}' must not be a symbolic link or reparse point.");
        }
    }

    private void TryDeleteProbe(string probePath)
    {
        try
        {
            if (_fileSystem.File.Exists(probePath))
            {
                _fileSystem.File.Delete(probePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [UnsupportedOSPlatform("windows")]
    private void EnsureUnixMode(string path, UnixFileMode expectedMode, string kind)
    {
        var actualMode = _fileSystem.File.GetUnixFileMode(path);
        if (actualMode != expectedMode)
        {
            throw new UnauthorizedAccessException(
                $"The Workspace state {kind} '{path}' must use Unix permissions '{Convert.ToString((int)expectedMode, 8)}'.");
        }
    }
}
