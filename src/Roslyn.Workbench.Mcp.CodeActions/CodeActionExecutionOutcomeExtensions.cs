namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class CodeActionExecutionOutcomeExtensions
{
    public static bool IsError(this CodeActionExecutionOutcome outcome)
    {
        return outcome is CodeActionExecutionOutcome.Rejected
            or CodeActionExecutionOutcome.Conflict
            or CodeActionExecutionOutcome.Faulted;
    }
}
