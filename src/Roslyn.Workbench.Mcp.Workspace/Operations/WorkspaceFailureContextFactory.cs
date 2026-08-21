namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal static class WorkspaceFailureContextFactory
{
    public static WorkspaceFailureContext Create(WorkspaceSessionSnapshot session)
    {
        var context = new WorkspaceFailureContext(
            session.Workspace,
            session.State,
            session.ProjectCount,
            session.DocumentCount,
            session.Transaction?.CurrentRevision);

        return context;
    }
}
