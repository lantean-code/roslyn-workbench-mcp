namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceOperationGate
{
    IAsyncDisposable? TryAcquireShared();

    IAsyncDisposable? TryAcquireExclusive();
}
