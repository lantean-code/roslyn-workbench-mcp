namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceChangeDetector
{
    WorkspaceInputManifest BuildManifest(Solution solution, string loadedPath);

    bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken);
}
