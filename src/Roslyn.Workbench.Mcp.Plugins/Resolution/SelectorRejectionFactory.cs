namespace Roslyn.Workbench.Mcp.Plugins.Resolution;

/// <summary>
/// Converts selector resolution statuses into consistent plugin rejection codes and recovery guidance.
/// </summary>
internal static class SelectorRejectionFactory
{
    /// <summary>
    /// Creates a target-specific rejection for an unsuccessful selector resolution.
    /// </summary>
    /// <typeparam name="TResponse">The tool response type.</typeparam>
    /// <param name="status">The unsuccessful selector resolution status.</param>
    /// <param name="targetCode">The target prefix used in the stable error code.</param>
    /// <param name="targetDisplayName">The target name used in the user-facing message.</param>
    /// <returns>A rejection instructing the caller to resolve the target again.</returns>
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
            SelectorResolveStatus.Invalid => (
                $"{targetCode}SelectorInvalid",
                $"The {targetDisplayName} selector contains an invalid path."),
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
