namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputCertification : IWorkspaceInputCertification
{
    private readonly IWorkspaceInputChangeMonitor _changeMonitor;
    private readonly StringComparer _pathComparer;
    private int _completionState;

    public WorkspaceInputCertification(
        IWorkspaceInputChangeMonitor changeMonitor,
        StringComparer pathComparer)
    {
        _changeMonitor = changeMonitor;
        _pathComparer = pathComparer;
        _changeMonitor.Start();
    }

    public WorkspaceInputManifest Complete(WorkspaceInputManifest manifest)
    {
        return Complete(manifest, []);
    }

    public WorkspaceInputManifest Complete(
        WorkspaceInputManifest manifest,
        IEnumerable<string> ignoredPaths)
    {
        var previousCompletionState = Interlocked.Exchange(ref _completionState, 1);
        if (previousCompletionState != 0)
        {
            throw new InvalidOperationException("Workspace input certification has already completed or been disposed.");
        }

        var ignoredPathSet = ignoredPaths.ToHashSet(_pathComparer);
        var completedManifest = new WorkspaceInputManifest
        {
            ChangeMonitor = _changeMonitor,
            Directories = manifest.Directories,
            EvaluationFailures = manifest.EvaluationFailures,
            Files = manifest.Files,
            IgnoredPaths = ignoredPathSet,
            PathPolicy = manifest.PathPolicy,
        };

        try
        {
            _changeMonitor.Track(completedManifest);
            return completedManifest;
        }
        catch
        {
            _changeMonitor.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        var previousCompletionState = Interlocked.Exchange(ref _completionState, 1);
        if (previousCompletionState == 0)
        {
            _changeMonitor.Dispose();
        }
    }
}
