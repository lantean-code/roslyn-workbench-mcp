namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Captures diagnostic context from the current state of a workspace session.
/// </summary>
internal static class WorkspaceFailureContextFactory
{
    /// <summary>
    /// Captures the workspace identity, lifecycle state, size, and transaction revision of a session.
    /// </summary>
    /// <param name="session">The workspace session in which the operation runs.</param>
    /// <returns>The context to attach to an unexpected workspace failure.</returns>
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
