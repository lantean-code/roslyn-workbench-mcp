using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the direction of transaction history movement.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransactionHistoryDirection>))]
public enum TransactionHistoryDirection
{
    /// <summary>
    /// Move to the prior transaction revision.
    /// </summary>
    Undo,

    /// <summary>
    /// Move to the next transaction revision.
    /// </summary>
    Redo,
}
