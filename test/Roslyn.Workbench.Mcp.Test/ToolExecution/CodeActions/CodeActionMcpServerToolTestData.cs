namespace Roslyn.Workbench.Mcp.Test.ToolExecution.CodeActions;

internal static class CodeActionMcpServerToolTestData
{
    public static CodeActionExecutionFailure CreateExecutionFailure(
        CodeActionExecutionOutcome outcome,
        string code)
    {
        return new CodeActionExecutionFailure
        {
            Outcome = outcome,
            Error = new CodeActionExecutionError
            {
                Code = code,
                Message = "Message",
            },
            RequiredAction = RequiredAction.Retry,
        };
    }
}
