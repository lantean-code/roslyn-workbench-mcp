namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceOperationGate
{
    IWorkspaceOperationLease? TryAcquireShared();

    IWorkspaceOperationLease? TryAcquireExclusive();
}
