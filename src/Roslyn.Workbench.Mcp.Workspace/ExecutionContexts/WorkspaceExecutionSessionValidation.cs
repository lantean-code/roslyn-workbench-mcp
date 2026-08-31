namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Couples the effective session state after validation with any execution rejection.
/// </summary>
internal sealed class WorkspaceExecutionSessionValidation
{
    /// <summary>
    /// Gets the effective session, including any state transition detected during validation.
    /// </summary>
    public WorkspaceSessionSnapshot Session { get; }

    /// <summary>
    /// Gets the execution failure when the session is unavailable for the requested operation.
    /// </summary>
    public WorkspaceExecutionFailure? Failure { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceExecutionSessionValidation"/> class.
    /// </summary>
    /// <param name="session">The effective session.</param>
    /// <param name="failure">The optional execution rejection.</param>
    public WorkspaceExecutionSessionValidation(
        WorkspaceSessionSnapshot session,
        WorkspaceExecutionFailure? failure = null)
    {
        Session = session;
        Failure = failure;
    }
}
