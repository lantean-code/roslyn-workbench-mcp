namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Identifies the normalized outcome of a Code Action handler invocation.
/// </summary>
internal enum CodeActionExecutionOutcome
{
    /// <summary>
    /// The handler completed successfully with data.
    /// </summary>
    Succeeded,
    /// <summary>
    /// The handler completed successfully without a source change.
    /// </summary>
    NoChange,
    /// <summary>
    /// The request was invalid or could not be fulfilled in the current state.
    /// </summary>
    Rejected,
    /// <summary>
    /// The request conflicted with stale or incompatible workspace state.
    /// </summary>
    Conflict,
    /// <summary>
    /// Execution failed unexpectedly.
    /// </summary>
    Faulted,
}
