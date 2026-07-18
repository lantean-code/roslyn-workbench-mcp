namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to roll back the active transaction.
/// </summary>
public sealed record TransactionRollbackRequest : WorkspaceBoundRequest;
