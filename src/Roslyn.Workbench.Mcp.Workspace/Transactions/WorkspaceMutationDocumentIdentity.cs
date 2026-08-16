namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceMutationDocumentIdentity
{
    public required Guid ProjectId { get; init; }

    public required FileSystemPathKey DocumentPath { get; init; }

    public required WorkspaceMutationDocumentChangeKind ChangeKind { get; init; }

    public required string ContentHash { get; init; }

    public required string SerializedBytesHash { get; init; }

    public required string EncodingName { get; init; }
}
