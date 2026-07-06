namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceChangeDetector : IWorkspaceChangeDetector
{
    public WorkspaceInputManifest BuildManifest(Solution solution, string loadedPath)
    {
        return WorkspaceInputManifestBuilder.Build(solution, loadedPath);
    }

    public bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken)
    {
        return WorkspaceInputManifestValidator.HasChanged(manifest, cancellationToken);
    }
}
