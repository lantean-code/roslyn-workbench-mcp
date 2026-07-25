namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to commit the active transaction.
/// </summary>
internal sealed record TransactionCommitRequest : WorkspaceMutationRequest;
