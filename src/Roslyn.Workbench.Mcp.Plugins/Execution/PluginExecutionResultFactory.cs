namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal static class PluginExecutionResultFactory
{
    public static PluginExecutionResult<TResponse> Rejected<TResponse>(
        string code,
        string message,
        RequiredAction? requiredAction = null)
    {
        var error = new PluginExecutionError
        {
            Code = code,
            Message = message,
        };

        return PluginExecutionResult<TResponse>.Rejected(error, requiredAction);
    }

    public static PluginExecutionResult<TResponse> RejectedFromStatus<TResponse>(
        SelectorResolveStatus status,
        string targetCode,
        string targetDisplayName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<TResponse>(
                $"{targetCode}Ambiguous",
                $"The {targetDisplayName} selector matched multiple results.",
                RequiredAction.ResolveTargetAgain),
            _ => Rejected<TResponse>(
                $"{targetCode}NotFound",
                $"The {targetDisplayName} selector did not match any result.",
                RequiredAction.ResolveTargetAgain),
        };
    }

    public static PluginExecutionResult<TResponse> ProjectStructureUnavailable<TResponse>(
        string message)
    {
        return Rejected<TResponse>(
            "ProjectStructureUnavailable",
            message,
            RequiredAction.Retry);
    }
}
