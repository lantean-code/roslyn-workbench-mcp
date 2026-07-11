using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IAtomicFileWriter
{
    ValueTask WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken);

    ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken);
}
