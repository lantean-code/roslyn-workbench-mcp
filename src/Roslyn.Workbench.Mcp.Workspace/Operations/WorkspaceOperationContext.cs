namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationContext
{
    public string? WorkspaceId { get; init; }

    public long? WorkspaceEpoch { get; init; }

    public int? TransactionRevision { get; init; }
}
