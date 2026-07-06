using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record TransactionPreviewOutcome
{
    public TransactionInfo Transaction { get; init; } = new();

    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    public DocumentDiff? Diff { get; init; }
}
