namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class WorkspaceDocumentContent
{
    public required ReadOnlyMemory<byte> SerializedBytes { get; init; }

    public required string ContentHash { get; init; }

    public required string SerializedBytesHash { get; init; }

    public required string EncodingName { get; init; }
}
