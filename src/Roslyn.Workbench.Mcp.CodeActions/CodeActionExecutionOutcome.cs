namespace Roslyn.Workbench.Mcp.CodeActions;

internal enum CodeActionExecutionOutcome
{
    Succeeded,
    NoChange,
    Rejected,
    Conflict,
    Faulted,
}
