namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record StagedMutation
{
    public required WorkspaceSessionSnapshot Session { get; init; }

    public required WorkspaceTransaction Transaction { get; init; }

    public required WorkspaceTransactionRevision Revision { get; init; }

    public required ChangeSummary Changes { get; init; }
}
