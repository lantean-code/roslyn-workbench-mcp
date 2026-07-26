namespace Roslyn.Workbench.Mcp.Plugins.Resolution;

internal static class SelectorRejectionFactory
{
    public static PluginExecutionResult<TResponse> Create<TResponse>(
        SelectorResolveStatus status,
        string targetCode,
        string targetDisplayName)
    {
        var (code, message) = status switch
        {
            SelectorResolveStatus.Ambiguous => (
                $"{targetCode}Ambiguous",
                $"The {targetDisplayName} selector matched multiple results."),
            _ => (
                $"{targetCode}NotFound",
                $"The {targetDisplayName} selector did not match any result."),
        };

        return PluginExecutionResult.Rejected<TResponse>(
            code,
            message,
            RequiredAction.ResolveTargetAgain);
    }
}
