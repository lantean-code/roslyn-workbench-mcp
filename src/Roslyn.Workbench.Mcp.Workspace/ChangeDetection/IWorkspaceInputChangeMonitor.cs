namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Watches Workspace-root inputs during loading and records the first relevant change.
/// </summary>
internal interface IWorkspaceInputChangeMonitor : IDisposable
{
    /// <summary>
    /// Gets the first observed input change, or <see langword="null"/> when none has occurred.
    /// </summary>
    WorkspaceInputChange? Change { get; }

    /// <summary>
    /// Starts the root watcher before manifest construction begins.
    /// </summary>
    void Start();

    /// <summary>
    /// Applies a completed manifest so subsequent events can be filtered against tracked inputs.
    /// </summary>
    /// <param name="manifest">The manifest whose files, directories and exclusions are relevant.</param>
    void Track(WorkspaceInputManifest manifest);

    /// <summary>
    /// Waits until watcher events already queued by the operating system have been processed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    void WaitForPendingEvents(CancellationToken cancellationToken);
}
