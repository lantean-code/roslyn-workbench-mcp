using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record TransactionPreviewOutcome
{
    public TransactionInfo Transaction { get; init; } = new();

    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    public DocumentDiff? Diff { get; init; }
}
