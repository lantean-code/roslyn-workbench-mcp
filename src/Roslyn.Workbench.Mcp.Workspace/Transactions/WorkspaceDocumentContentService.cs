using System.Security.Cryptography;
using System.Text;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Serializes Roslyn document text and calculates identities used by mutation preconditions.
/// </summary>
internal sealed class WorkspaceDocumentContentService : IWorkspaceDocumentContentService
{
    /// <summary>
    /// Serializes a document while preserving encoding and calculates its content hashes.
    /// </summary>
    /// <param name="document">The document whose serialized content is required.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the workspace document content.</returns>
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

    /// <summary>
    /// Determines whether two document-content snapshots have equivalent text and encoding.
    /// </summary>
    /// <param name="expected">The expected document content to compare with the actual content.</param>
    /// <param name="candidate">The candidate document content to compare.</param>
    /// <returns><see langword="true"/> when equivalent content; otherwise, <see langword="false"/>.</returns>
    public bool HasEquivalentContent(
        WorkspaceDocumentContent expected,
        WorkspaceDocumentContent candidate)
    {
        return string.Equals(expected.ContentHash, candidate.ContentHash, StringComparison.Ordinal)
            && string.Equals(expected.SerializedBytesHash, candidate.SerializedBytesHash, StringComparison.Ordinal)
            && string.Equals(expected.EncodingName, candidate.EncodingName, StringComparison.Ordinal);
    }
}
