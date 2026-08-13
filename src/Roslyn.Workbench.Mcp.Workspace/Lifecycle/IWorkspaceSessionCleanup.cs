namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal interface IWorkspaceSessionCleanup
{
    ValueTask CleanupAsync(WorkspaceSessionSnapshot session);
}
