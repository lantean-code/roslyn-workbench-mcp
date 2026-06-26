using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned when a transaction starts.
/// </summary>
public sealed record TransactionStartData
{
    /// <summary>
    /// Gets the active transaction info.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }
}
