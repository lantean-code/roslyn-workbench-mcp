namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to commit the active transaction.
/// </summary>
internal sealed record TransactionCommitRequest : WorkspaceMutationRequest;
