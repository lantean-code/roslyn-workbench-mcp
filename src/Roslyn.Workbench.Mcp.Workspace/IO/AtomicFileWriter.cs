using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Writes durable contents to a temporary file before atomically committing the destination.
/// </summary>
internal sealed class AtomicFileWriter : IAtomicFileWriter
{
    private const UnixFileMode _ownerOnlyFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly TimeSpan[] _commitRetryDelays =
    [
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileCommitter _fileCommitter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicFileWriter"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system used to create and clean temporary files.</param>
    /// <param name="fileCommitter">The platform-specific durable committer.</param>
    public AtomicFileWriter(IFileSystem fileSystem, IAtomicFileCommitter fileCommitter)
    {
        _fileSystem = fileSystem;
        _fileCommitter = fileCommitter;
    }

    /// <inheritdoc/>
    public async ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        AtomicFileAccess access,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        await WriteAllBytesCoreAsync(
            destinationPath,
            encoding.GetBytes(contents),
            access,
            unixFileMode: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        CancellationToken cancellationToken)
    {
        return WriteAllBytesCoreAsync(
            destinationPath,
            contents,
            access,
            unixFileMode: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        UnixFileMode? unixFileMode,
        CancellationToken cancellationToken)
    {
        return WriteAllBytesCoreAsync(
            destinationPath,
            contents,
            access,
            unixFileMode,
            cancellationToken);
    }

    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "FileStream.FlushAsync does not expose flushToDisk; the synchronous flush is required before the atomic commit to guarantee durable storage.")]
    private async ValueTask WriteAllBytesCoreAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        UnixFileMode? unixFileMode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (access is not AtomicFileAccess.Default and not AtomicFileAccess.OwnerOnly)
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "The atomic file access policy is not supported.");
        }

        if (unixFileMode is not null && OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unix file permissions cannot be applied on Windows.");
        }

        if (unixFileMode is not null && access == AtomicFileAccess.OwnerOnly)
        {
            throw new ArgumentException(
                "Explicit Unix file permissions cannot be combined with owner-only access.",
                nameof(unixFileMode));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = _fileSystem.Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException("The destination path must have a parent directory.", nameof(destinationPath));

        var temporaryPath = _fileSystem.Path.Combine(
            directoryPath,
            $".{_fileSystem.Path.GetFileName(destinationPath)}.{Guid.NewGuid():n}.tmp");

        try
        {
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                Share = FileShare.None,
            };

            if (unixFileMode is not null)
            {
                options.UnixCreateMode = unixFileMode;
            }
            else if (access == AtomicFileAccess.OwnerOnly && !OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = _ownerOnlyFileMode;
            }

            await using (var stream = _fileSystem.FileStream.New(temporaryPath, options))
            {
                if (unixFileMode is { } exactUnixFileMode)
                {
                    _fileSystem.File.SetUnixFileMode(temporaryPath, exactUnixFileMode);
                }

                await stream.WriteAsync(contents, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            await CommitWithRetryAsync(temporaryPath, destinationPath, cancellationToken);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    private async ValueTask CommitWithRetryAsync(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                _fileCommitter.Commit(temporaryPath, destinationPath);
                return;
            }
            catch (AtomicFileCommitException exception) when (exception.IsRetryable && attempt < _commitRetryDelays.Length)
            {
                var retryDelay = _commitRetryDelays[attempt];
                WorkbenchPerformanceEventSource.Log.AtomicFileCommitRetry(
                    retryNumber: attempt + 1,
                    delayMilliseconds: (int)retryDelay.TotalMilliseconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (_fileSystem.File.Exists(temporaryPath))
            {
                _fileSystem.File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
