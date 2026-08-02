namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceChangeDetector
{
    IWorkspaceInputCertification BeginCertification(string workspaceRoot);

    WorkspaceInputManifest BuildManifest(Solution solution, string loadedPath, string workspaceRoot);

    WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputCertification certification);

    bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken);
}
