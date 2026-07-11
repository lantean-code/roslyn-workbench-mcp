using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed partial class NativeAtomicFileCommitter : IAtomicFileCommitter
{
    private const uint _moveFileReplaceExisting = 0x1;
    private const uint _moveFileWriteThrough = 0x8;

    public void Commit(string temporaryPath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (OperatingSystem.IsWindows())
        {
            if (MoveFileEx(temporaryPath, destinationPath, _moveFileReplaceExisting | _moveFileWriteThrough) == 0)
            {
                throw new IOException(
                    $"The atomic replacement of '{destinationPath}' failed.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return;
        }

        File.Move(temporaryPath, destinationPath, overwrite: true);
        SyncDirectory(Path.GetDirectoryName(destinationPath)!);
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (OperatingSystem.IsWindows())
        {
            if (MoveFileEx(sourcePath, destinationPath, _moveFileWriteThrough) == 0)
            {
                throw new IOException(
                    $"The durable move from '{sourcePath}' to '{destinationPath}' failed.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            return;
        }

        File.Move(sourcePath, destinationPath);
        SyncDirectory(Path.GetDirectoryName(destinationPath)!);
    }

    private static void SyncDirectory(string directoryPath)
    {
        var fileDescriptor = Open(directoryPath, 0);
        if (fileDescriptor < 0)
        {
            throw new IOException(
                $"The directory '{directoryPath}' could not be opened for durability synchronisation.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        try
        {
            if (Fsync(fileDescriptor) != 0)
            {
                throw new IOException(
                    $"The directory '{directoryPath}' could not be synchronised after atomic replacement.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }
        }
        finally
        {
            _ = Close(fileDescriptor);
        }
    }

    [LibraryImport("kernel32", EntryPoint = "MoveFileExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial int MoveFileEx(string existingFileName, string newFileName, uint flags);

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int Close(int fileDescriptor);
}
