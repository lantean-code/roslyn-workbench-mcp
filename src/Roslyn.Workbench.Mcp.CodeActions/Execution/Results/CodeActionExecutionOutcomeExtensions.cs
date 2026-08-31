namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Classifies Code Action execution outcomes.
/// </summary>
internal static class CodeActionExecutionOutcomeExtensions
{
    /// <summary>
    /// Determines whether the outcome represents an execution error.
    /// </summary>
    /// <param name="outcome">The tool outcome to classify.</param>
    /// <returns><see langword="true"/> for rejected, conflicting, or faulted outcomes; otherwise, <see langword="false"/>.</returns>
    public static bool IsError(this CodeActionExecutionOutcome outcome)
    {
        return outcome is CodeActionExecutionOutcome.Rejected
            or CodeActionExecutionOutcome.Conflict
            or CodeActionExecutionOutcome.Faulted;
    }
}
