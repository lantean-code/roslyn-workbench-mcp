using System.Runtime.Versioning;

namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal sealed class WorkspaceStateDirectorySecurity : IWorkspaceStateDirectorySecurity
{
    private const UnixFileMode _privateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode _privateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly IFileSystem _fileSystem;

    public WorkspaceStateDirectorySecurity(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

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

    public void ValidateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureNotRedirected(path, "directory");
        if (!OperatingSystem.IsWindows())
        {
            EnsureUnixMode(path, _privateDirectoryMode, "directory");
        }
    }

    public void ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureNotRedirected(path, "file");
        if (!OperatingSystem.IsWindows())
        {
            EnsureUnixMode(path, _privateFileMode, "file");
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
