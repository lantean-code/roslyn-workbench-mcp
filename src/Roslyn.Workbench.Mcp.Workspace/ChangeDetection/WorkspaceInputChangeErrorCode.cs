namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Classifies failures that prevent reliable monitoring of Workspace inputs.
/// </summary>
internal enum WorkspaceInputChangeErrorCode
{
    /// <summary>
    /// The operating-system watcher lost events because its internal buffer overflowed.
    /// </summary>
    WatcherBufferOverflow,

    /// <summary>
    /// A watcher failed for a reason other than buffer overflow.
    /// </summary>
    WatcherFailure,

    /// <summary>
    /// External item membership could not be enumerated reliably.
    /// </summary>
    MembershipEnumerationFailure,
}
