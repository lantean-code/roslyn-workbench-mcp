namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Watches evaluated inputs outside the Workspace root and records the first relevant change.
/// </summary>
internal interface IWorkspaceExternalInputChangeMonitor : IDisposable
{
    /// <summary>
    /// Gets the first observed external-input change, or <see langword="null"/> when none has occurred.
    /// </summary>
    WorkspaceInputChange? Change { get; }

    /// <summary>
    /// Starts all configured external-input watchers.
    /// </summary>
    void Start();

    /// <summary>
    /// Waits until watcher events already queued by the operating system have been processed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    void WaitForPendingEvents(CancellationToken cancellationToken);
}
