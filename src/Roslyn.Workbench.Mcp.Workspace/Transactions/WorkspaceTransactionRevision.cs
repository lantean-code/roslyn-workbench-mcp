using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record WorkspaceTransactionRevision
{
    public Solution Solution { get; init; } = null!;

    public ChangeSummary Changes { get; init; } = new();

    public string Operation { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public MutationPreview Preview { get; init; } = new();
}
