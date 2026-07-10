using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to start a transaction.
/// </summary>
public sealed record TransactionStartRequest : WorkspaceBoundRequest
{ }
