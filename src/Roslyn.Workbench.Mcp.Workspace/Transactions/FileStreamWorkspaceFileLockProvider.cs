using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Acquires an exclusive workspace commit lock by holding an open file stream.
/// </summary>
internal sealed partial class FileStreamWorkspaceFileLockProvider : IWorkspaceFileLockProvider
{
    private const int _macOsExclusiveLock = 2;
    private const int _macOsNonBlockingLock = 4;
    private const int _macOsUnlock = 8;
    private const int _macOsWouldBlock = 35;

    /// <summary>
    /// Attempts to acquire exclusive ownership of a lock file.
    /// </summary>
    /// <param name="lockPath">The path of the workspace ownership lock to acquire.</param>
    /// <returns>The owned lock, or <see langword="null"/> when the lock is unavailable.</returns>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public IWorkspaceCommitLock? TryAcquire(string lockPath)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Workspace commit locking is supported on Windows, Linux and macOS.");
        }

        var stream = OpenLockFile(lockPath);
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                return TryAcquireMacOs(stream);
            }

            stream.Lock(0, 1);
            return new FileStreamWorkspaceFileLock(stream);
        }
        catch (IOException)
        {
            stream.Dispose();
            return null;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static FileStream OpenLockFile(string lockPath)
    {
        var stream = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.WriteThrough);

        if (stream.Length == 0)
        {
            stream.SetLength(1);
            stream.Flush(flushToDisk: true);
        }

        return stream;
    }

    [SupportedOSPlatform("macos")]
    private static MacOsWorkspaceFileLock? TryAcquireMacOs(FileStream stream)
    {
        if (FlockMacOs(
            stream.SafeFileHandle.DangerousGetHandle().ToInt32(),
            _macOsExclusiveLock | _macOsNonBlockingLock) == 0)
        {
            return new MacOsWorkspaceFileLock(stream);
        }

        var error = Marshal.GetLastPInvokeError();
        if (error == _macOsWouldBlock)
        {
            stream.Dispose();
            return null;
        }

        throw new IOException(
            "The macOS workspace commit lock could not be acquired.",
            new Win32Exception(error));
    }

    [SuppressMessage("Security", "CA5392", Justification = "DefaultDllImportSearchPaths has no effect on non-Windows platforms; this import targets the platform system library.")]
    [LibraryImport("libSystem.dylib", EntryPoint = "flock", SetLastError = true)]
    private static partial int FlockMacOs(int fileDescriptor, int operation);

    private sealed class FileStreamWorkspaceFileLock : IWorkspaceCommitLock
    {
        private readonly FileStream _stream;

        public FileStreamWorkspaceFileLock(FileStream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                _stream.Dispose();
                return;
            }

            try
            {
                _stream.Unlock(0, 1);
            }
            finally
            {
                _stream.Dispose();
            }
        }
    }

    [SupportedOSPlatform("macos")]
    private sealed class MacOsWorkspaceFileLock : IWorkspaceCommitLock
    {
        private readonly FileStream _stream;

        public MacOsWorkspaceFileLock(FileStream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            try
            {
                if (FlockMacOs(_stream.SafeFileHandle.DangerousGetHandle().ToInt32(), _macOsUnlock) != 0)
                {
                    throw new IOException(
                        "The macOS workspace commit lock could not be released.",
                        new Win32Exception(Marshal.GetLastPInvokeError()));
                }
            }
            finally
            {
                _stream.Dispose();
            }
        }
    }
}
