namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed class WorkspaceExecutionSessionValidation
{
    public WorkspaceSessionSnapshot Session { get; }

    public WorkspaceExecutionFailure? Failure { get; }

    public WorkspaceExecutionSessionValidation(
        WorkspaceSessionSnapshot session,
        WorkspaceExecutionFailure? failure = null)
    {
        Session = session;
        Failure = failure;
    }
}
