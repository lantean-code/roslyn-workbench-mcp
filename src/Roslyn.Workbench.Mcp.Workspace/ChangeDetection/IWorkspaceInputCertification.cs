namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal interface IWorkspaceInputCertification : IDisposable
{
    WorkspaceInputManifest Complete(WorkspaceInputManifest manifest);

    WorkspaceInputManifest Complete(
        WorkspaceInputManifest manifest,
        IEnumerable<string> ignoredPaths);
}
