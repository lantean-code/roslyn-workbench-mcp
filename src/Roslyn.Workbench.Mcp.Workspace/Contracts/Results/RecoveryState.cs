using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents the state of commit recovery.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecoveryState>))]
public enum RecoveryState
{
    /// <summary>
    /// Recovery state has been prepared.
    /// </summary>
    Prepared,

    /// <summary>
    /// Recovery is currently applying changes.
    /// </summary>
    Applying,

    /// <summary>
    /// Recovery completed and the change was committed.
    /// </summary>
    Committed,

    /// <summary>
    /// Recovery restored the prior state.
    /// </summary>
    Restored,

    /// <summary>
    /// Recovery detected an unresolved conflict.
    /// </summary>
    RecoveryConflict,

    /// <summary>
    /// Recovery did not complete successfully.
    /// </summary>
    RecoveryIncomplete,
}
