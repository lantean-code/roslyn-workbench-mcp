using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IAtomicFileWriter
{
    ValueTask WriteAllTextAsync(
        string destinationPath,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken);
}
