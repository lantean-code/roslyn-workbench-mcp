namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the state of commit recovery. Numeric values are part of the durable recovery format.
/// </summary>
public enum RecoveryState
{
    /// <summary>
    /// Recovery state has been prepared.
    /// </summary>
    Prepared = 0,

    /// <summary>
    /// Recovery is currently applying changes.
    /// </summary>
    Applying = 1,

    /// <summary>
    /// Recovery completed and the change was committed.
    /// </summary>
    Committed = 2,

    /// <summary>
    /// Recovery restored the prior state.
    /// </summary>
    Restored = 3,

    /// <summary>
    /// Recovery detected an unresolved conflict.
    /// </summary>
    RecoveryConflict = 4,

    /// <summary>
    /// Recovery did not complete successfully.
    /// </summary>
    RecoveryIncomplete = 5,
}
