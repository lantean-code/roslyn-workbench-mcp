using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IAtomicFileWriter
{
    ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        AtomicFileAccess access,
        CancellationToken cancellationToken);

    ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        AtomicFileAccess access,
        CancellationToken cancellationToken);
}
