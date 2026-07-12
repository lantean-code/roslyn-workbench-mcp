namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceSessionAcquirer
{
    WorkspaceSessionAcquisition AcquireShared(WorkspaceSelector? selector);

    WorkspaceSessionAcquisition AcquireExclusive(WorkspaceSelector? selector);
}
