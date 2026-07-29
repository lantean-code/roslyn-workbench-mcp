namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal static class ToolExecutionHelpers
{
    public static int GetMaxResults(int? requestLimit, int defaultMaxResults)
    {
        return Math.Max(0, requestLimit ?? defaultMaxResults);
    }
}
