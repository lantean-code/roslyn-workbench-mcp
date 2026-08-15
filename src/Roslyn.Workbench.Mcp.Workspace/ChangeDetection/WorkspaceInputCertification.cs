namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceInputCertification : IWorkspaceInputCertification
{
    private readonly IWorkspaceInputChangeMonitor _changeMonitor;
    private readonly IWorkspacePathComparison _pathComparison;
    private int _completionState;

    public WorkspaceInputCertification(
        IWorkspaceInputChangeMonitor changeMonitor,
        IWorkspacePathComparison pathComparison)
    {
        _changeMonitor = changeMonitor;
        _pathComparison = pathComparison;
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

        var ignoredPathSet = ignoredPaths
            .Select(_pathComparison.CreateKey)
            .ToHashSet();
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
