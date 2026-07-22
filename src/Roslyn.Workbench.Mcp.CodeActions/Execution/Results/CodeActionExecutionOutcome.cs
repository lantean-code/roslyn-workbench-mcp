namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal enum CodeActionExecutionOutcome
{
    Succeeded,
    NoChange,
    Rejected,
    Conflict,
    Faulted,
}
