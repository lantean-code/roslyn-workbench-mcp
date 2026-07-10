namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

internal static class PluginMcpServerToolTestData
{
    public static ToolExecutionFailureResult CreateExecutionFailure(
        PluginExecutionOutcome outcome,
        string code)
    {
        return new ToolExecutionFailureResult
        {
            Outcome = outcome,
            Error = new PluginExecutionError
            {
                Code = code,
                Message = "Message",
            },
            RequiredAction = RequiredAction.Retry,
        };
    }
}
