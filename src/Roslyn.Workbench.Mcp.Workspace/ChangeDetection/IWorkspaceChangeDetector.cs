namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceChangeDetector
{
    IWorkspaceInputCertification BeginCertification(string workspaceRoot);

    WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties = null);

    WorkspaceInputManifest BuildManifest(
        Solution solution,
        string loadedPath,
        string workspaceRoot,
        IWorkspaceInputCertification certification,
        WorkspaceMsBuildProperties? msBuildProperties = null);

    bool HasChanged(WorkspaceInputManifest manifest, CancellationToken cancellationToken);
}
