using System.Security.Cryptography;
using System.Text;

using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.TestSupport.Workspace;

internal static class WorkspaceDocumentContentTestFactory
{
    public static async ValueTask<WorkspaceDocumentContent> CreateAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken);
        var encoding = text.Encoding ?? Encoding.UTF8;
        var serializedText = text.ToString();
        byte[] serializedBytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(serializedText)];
        var documentContent = new WorkspaceDocumentContent
        {
            SerializedBytes = serializedBytes,
            ContentHash = Convert.ToHexString(text.GetContentHash().AsSpan()),
            SerializedBytesHash = Convert.ToHexString(SHA256.HashData(serializedBytes)),
            EncodingName = encoding.WebName,
        };

        return documentContent;
    }

    public static bool HasEquivalentContent(
        WorkspaceDocumentContent expected,
        WorkspaceDocumentContent candidate)
    {
        return string.Equals(expected.ContentHash, candidate.ContentHash, StringComparison.Ordinal)
            && string.Equals(expected.SerializedBytesHash, candidate.SerializedBytesHash, StringComparison.Ordinal)
            && string.Equals(expected.EncodingName, candidate.EncodingName, StringComparison.Ordinal);
    }
}
