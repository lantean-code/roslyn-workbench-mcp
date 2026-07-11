using System.Runtime.Versioning;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class FileStreamWorkspaceFileLockProvider : IWorkspaceFileLockProvider
{
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    public IWorkspaceCommitLock? TryAcquire(string lockPath)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Workspace commit locking is supported on Windows and Linux.");
        }

        var stream = OpenLockFile(lockPath);
        try
        {
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
}
