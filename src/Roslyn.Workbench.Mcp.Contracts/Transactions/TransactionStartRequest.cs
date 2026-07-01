using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to start a transaction.
/// </summary>
public sealed record TransactionStartRequest : WorkspaceBoundRequest
{ }
