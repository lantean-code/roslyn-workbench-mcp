
namespace Roslyn.Workbench.Mcp.Workspace.State;

internal interface IWorkspaceStateTransitions
{
    WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger);

    WorkspaceSessionSnapshot ApplyExternalChangeDetected(WorkspaceSessionSnapshot session);
}
