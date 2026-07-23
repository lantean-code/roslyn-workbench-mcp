using System.ComponentModel;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed partial class NativeAtomicFileCommitter : IAtomicFileCommitter
{
    private const string _extendedPathPrefix = @"\\?\";
    private const string _extendedUncPathPrefix = @"\\?\UNC\";
    private const string _uncPathPrefix = @"\\";
    private const uint _moveFileReplaceExisting = 0x1;
    private const uint _moveFileWriteThrough = 0x8;

    public void Commit(string temporaryPath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (OperatingSystem.IsWindows())
        {
            MoveWindows(
                temporaryPath,
                destinationPath,
                _moveFileReplaceExisting | _moveFileWriteThrough,
                $"The atomic replacement of '{destinationPath}' failed.");

            return;
        }

        MoveUnix(temporaryPath, destinationPath, overwrite: true);
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (OperatingSystem.IsWindows())
        {
            MoveWindows(
                sourcePath,
                destinationPath,
                _moveFileWriteThrough,
                $"The durable move from '{sourcePath}' to '{destinationPath}' failed.");

            return;
        }

        MoveUnix(sourcePath, destinationPath, overwrite: false);
    }

    private static void MoveWindows(string sourcePath, string destinationPath, uint flags, string failureMessage)
    {
        var extendedSourcePath = GetExtendedWindowsPath(sourcePath);
        var extendedDestinationPath = GetExtendedWindowsPath(destinationPath);

        if (MoveFileEx(extendedSourcePath, extendedDestinationPath, flags) == 0)
        {
            throw new IOException(failureMessage, new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    private static string GetExtendedWindowsPath(string path)
    {
        if (path.StartsWith(_extendedPathPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(_uncPathPrefix, StringComparison.Ordinal)
            ? _extendedUncPathPrefix + fullPath[_uncPathPrefix.Length..]
            : _extendedPathPrefix + fullPath;
    }

    private static void MoveUnix(string sourcePath, string destinationPath, bool overwrite)
    {
        File.Move(sourcePath, destinationPath, overwrite);
        SyncDirectory(GetRequiredDirectoryName(destinationPath));
    }

    private static void SyncDirectory(string directoryPath)
    {
        var rawFileDescriptor = Open(directoryPath, 0);
        if (rawFileDescriptor < 0)
        {
            throw new IOException(
                $"The directory '{directoryPath}' could not be opened for durability synchronisation.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        using var fileDescriptor = new SafeFileHandle((IntPtr)rawFileDescriptor, ownsHandle: true);
        if (Fsync(fileDescriptor.DangerousGetHandle().ToInt32()) != 0)
        {
            throw new IOException(
                $"The directory '{directoryPath}' could not be synchronised after atomic replacement.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    private static string GetRequiredDirectoryName(string path)
    {
        return Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"The path '{path}' does not have a parent directory.");
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("kernel32", EntryPoint = "MoveFileExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial int MoveFileEx(string existingFileName, string newFileName, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static partial int Fsync(int fileDescriptor);

}
