namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record MutationStagingOutcome
{
    public string Operation { get; init; } = string.Empty;

    public required string Summary { get; init; }

    public TransactionInfo Transaction { get; init; } = new();

    public required MutationPreview Preview { get; init; }

    public ChangeSummary Changes { get; init; } = new();
}
