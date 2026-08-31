using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Writes durable file contents through a temporary file and atomic commit.
/// </summary>
internal interface IAtomicFileWriter
{
    /// <summary>
    /// Atomically writes bytes using the selected access policy.
    /// </summary>
    /// <param name="destinationPath">The path to which the file will be written.</param>
    /// <param name="contents">The bytes to write.</param>
    /// <param name="access">The access policy for a newly created destination.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    /// <returns>A task that completes when the file has been durably committed.</returns>
    ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically writes bytes with optional explicit Unix permissions.
    /// </summary>
    /// <param name="destinationPath">The path to which the file will be written.</param>
    /// <param name="contents">The bytes to write.</param>
    /// <param name="access">The access policy for a newly created destination.</param>
    /// <param name="unixFileMode">The exact Unix permissions, when required.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    /// <returns>A task that completes when the file has been durably committed.</returns>
    ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        UnixFileMode? unixFileMode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Encodes and atomically writes text using the selected access policy.
    /// </summary>
    /// <param name="destinationPath">The path to which the file will be written.</param>
    /// <param name="contents">The text to write.</param>
    /// <param name="encoding">The encoding used to produce the file bytes.</param>
    /// <param name="access">The access policy for a newly created destination.</param>
    /// <param name="cancellationToken">The token that cancels the write.</param>
    /// <returns>A task that completes when the file has been durably committed.</returns>
    ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        AtomicFileAccess access,
        CancellationToken cancellationToken);
}
