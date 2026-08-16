using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceDocumentContentService : IWorkspaceDocumentContentService
{
    public async ValueTask<WorkspaceDocumentContent> CreateAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = await document.GetTextAsync(cancellationToken);
        var encoding = text.Encoding ?? Encoding.UTF8;
        var serializedText = text.ToString();
        var preamble = encoding.GetPreamble();
        var serializedBytes = new byte[preamble.Length + encoding.GetByteCount(serializedText)];
        preamble.CopyTo(serializedBytes, 0);
        encoding.GetBytes(serializedText, 0, serializedText.Length, serializedBytes, preamble.Length);

        return new WorkspaceDocumentContent
        {
            SerializedBytes = serializedBytes,
            ContentHash = Convert.ToHexString(text.GetContentHash().AsSpan()),
            SerializedBytesHash = Convert.ToHexString(SHA256.HashData(serializedBytes)),
            EncodingName = encoding.WebName,
        };
    }

    public bool HasEquivalentContent(
        WorkspaceDocumentContent expected,
        WorkspaceDocumentContent candidate)
    {
        return string.Equals(expected.ContentHash, candidate.ContentHash, StringComparison.Ordinal)
            && string.Equals(expected.SerializedBytesHash, candidate.SerializedBytesHash, StringComparison.Ordinal)
            && string.Equals(expected.EncodingName, candidate.EncodingName, StringComparison.Ordinal);
    }
}
