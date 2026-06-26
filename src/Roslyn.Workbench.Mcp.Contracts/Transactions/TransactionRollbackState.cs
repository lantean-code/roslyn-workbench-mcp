using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the resulting workspace state after rollback.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TransactionRollbackState>))]
public enum TransactionRollbackState
{
    /// <summary>
    /// The workspace is ready after rollback.
    /// </summary>
    Ready,

    /// <summary>
    /// The workspace remains out of date after rollback.
    /// </summary>
    WorkspaceOutOfDate,
}
