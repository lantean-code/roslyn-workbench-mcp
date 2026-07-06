using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Workspace;

internal interface IWorkspaceStateTransitions
{
    WorkspaceLifecycleState Fire(WorkspaceLifecycleState state, WorkspaceTrigger trigger);

    WorkspaceSessionSnapshot ApplyExternalChangeDetected(WorkspaceSessionSnapshot session);
}
