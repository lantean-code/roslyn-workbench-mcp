namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceInputManifest : IDisposable
{
    private WorkspaceInputChange? _change;

    public IWorkspaceInputChangeMonitor? ChangeMonitor { get; init; }

    public WorkspaceInputChange? Change => Volatile.Read(ref _change);

    public IReadOnlyList<WorkspaceInputDirectoryFingerprint> Directories { get; init; } = [];

    public IReadOnlyList<WorkspaceProjectInputFailure> EvaluationFailures { get; init; } = [];

    public IReadOnlyList<WorkspaceInputFileFingerprint> Files { get; init; } = [];

    public IReadOnlySet<string> IgnoredPaths { get; init; } = new HashSet<string>();

    public WorkspaceInputPathPolicy PathPolicy { get; init; } = WorkspaceInputPathPolicy.TrackAll;

    public bool IsComplete => EvaluationFailures.Count == 0;

    public void Dispose()
    {
        ChangeMonitor?.Dispose();
    }

    internal void RecordChange(WorkspaceInputChange change)
    {
        Interlocked.CompareExchange(ref _change, change, null);
    }
}
