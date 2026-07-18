namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed record MutationStagingOutcome
{
    public string Operation { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public TransactionInfo Transaction { get; init; } = new();

    public MutationPreview Preview { get; init; } = new();

    public ChangeSummary Changes { get; init; } = new();
}
