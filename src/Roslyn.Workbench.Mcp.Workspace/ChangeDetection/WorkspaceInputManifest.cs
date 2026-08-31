namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Represents the certified files, directories and evaluated memberships whose stability underpins a loaded Workspace.
/// </summary>
internal sealed record WorkspaceInputManifest : IDisposable
{
    private WorkspaceInputChange? _change;

    /// <summary>
    /// Gets the active root-input monitor retained for the lifetime of the loaded Workspace.
    /// </summary>
    public IWorkspaceInputChangeMonitor? ChangeMonitor { get; init; }

    /// <summary>
    /// Gets the first change observed after or during manifest certification.
    /// </summary>
    public WorkspaceInputChange? Change => Volatile.Read(ref _change);

    /// <summary>
    /// Gets directories whose existence is part of the certified input set.
    /// </summary>
    public IReadOnlyList<WorkspaceInputDirectoryFingerprint> Directories { get; init; } = [];

    /// <summary>
    /// Gets project-input evaluation failures that make the manifest incomplete.
    /// </summary>
    public IReadOnlyList<WorkspaceProjectInputFailure> EvaluationFailures { get; init; } = [];

    /// <summary>
    /// Gets evaluated item memberships rooted outside the trusted Workspace boundary.
    /// </summary>
    public IReadOnlyList<WorkspaceExternalInputMembership> ExternalInputMemberships { get; init; } = [];

    /// <summary>
    /// Gets file metadata captured when the manifest was certified.
    /// </summary>
    public IReadOnlyList<WorkspaceInputFileFingerprint> Files { get; init; } = [];

    /// <summary>
    /// Gets paths whose watcher events are intentionally ignored for this manifest.
    /// </summary>
    public IReadOnlySet<FileSystemPathKey> IgnoredPaths { get; init; } = new HashSet<FileSystemPathKey>();

    /// <summary>
    /// Gets the policy used to exclude irrelevant directory subtrees from monitoring.
    /// </summary>
    public WorkspaceInputPathPolicy PathPolicy { get; init; } = WorkspaceInputPathPolicy.MonitorAll;

    /// <summary>
    /// Gets whether every project contributed a successfully evaluated input set.
    /// </summary>
    public bool IsComplete => EvaluationFailures.Count == 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        ChangeMonitor?.Dispose();
    }

    /// <summary>
    /// Records a change only when no earlier invalidating change has already won the race.
    /// </summary>
    /// <param name="change">The detected change.</param>
    internal void RecordChange(WorkspaceInputChange change)
    {
        Interlocked.CompareExchange(ref _change, change, null);
    }
}
