using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed class AtomicFileWriter : IAtomicFileWriter
{
    private readonly IFileSystem _fileSystem;
    private readonly IAtomicFileCommitter _fileCommitter;

    public AtomicFileWriter(IFileSystem fileSystem, IAtomicFileCommitter fileCommitter)
    {
        _fileSystem = fileSystem;
        _fileCommitter = fileCommitter;
    }

    public async ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        await WriteAllBytesAsync(destinationPath, encoding.GetBytes(contents), cancellationToken);
    }

    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "FileStream.FlushAsync does not expose flushToDisk; the synchronous flush is required before the atomic commit to guarantee durable storage.")]
    public async ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
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
            await using (var stream = _fileSystem.FileStream.New(temporaryPath, options))
            {
                await stream.WriteAsync(contents, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            _fileCommitter.Commit(temporaryPath, destinationPath);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
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
