namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Captures serialized document bytes, text identity, and encoding for commit comparison.
/// </summary>
internal sealed class WorkspaceDocumentContent
{
    /// <summary>
    /// Gets the document bytes produced with the effective encoding.
    /// </summary>
    public required ReadOnlyMemory<byte> SerializedBytes { get; init; }

    /// <summary>
    /// Gets the hash of normalized Roslyn text content.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Gets the hash of the exact serialized bytes.
    /// </summary>
    public required string SerializedBytesHash { get; init; }

    /// <summary>
    /// Gets the web name of the effective text encoding.
    /// </summary>
    public required string EncodingName { get; init; }
}
