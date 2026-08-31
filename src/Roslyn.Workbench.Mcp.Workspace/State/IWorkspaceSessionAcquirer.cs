namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Selects a loaded Workspace session and acquires the operation ownership required by a caller.
/// </summary>
internal interface IWorkspaceSessionAcquirer
{
    /// <summary>
    /// Selects a session and attempts to acquire shared read ownership.
    /// </summary>
    /// <param name="selector">The optional Workspace selector.</param>
    /// <returns>The acquired session and lease, or a structured selection or contention failure.</returns>
    WorkspaceSessionAcquisition AcquireShared(WorkspaceSelector? selector);

    /// <summary>
    /// Selects a session and attempts to acquire exclusive lifecycle or mutation ownership.
    /// </summary>
    /// <param name="selector">The optional Workspace selector.</param>
    /// <returns>The acquired session and lease, or a structured selection or contention failure.</returns>
    WorkspaceSessionAcquisition AcquireExclusive(WorkspaceSelector? selector);
}
