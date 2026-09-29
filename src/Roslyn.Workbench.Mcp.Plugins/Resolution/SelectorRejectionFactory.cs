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
        var message = status switch
        {
            SelectorResolveStatus.Ambiguous => $"The {targetDisplayName} selector matched multiple results.",
            SelectorResolveStatus.Invalid => $"The {targetDisplayName} selector contains an invalid path.",
            _ => $"The {targetDisplayName} selector did not match any result.",
        };

        var code = ResolveErrorCode(targetCode, status);

        return PluginExecutionResult.Rejected<TResponse>(
            code,
            message,
            RequiredAction.ResolveTargetAgain);
    }

    private static string ResolveErrorCode(string targetCode, SelectorResolveStatus status)
    {
        return (targetCode, status) switch
        {
            ("Document", SelectorResolveStatus.Ambiguous) => PluginErrorCodes.DocumentAmbiguous,
            ("Document", SelectorResolveStatus.Invalid) => PluginErrorCodes.DocumentSelectorInvalid,
            ("Document", _) => PluginErrorCodes.DocumentNotFound,
            ("Project", SelectorResolveStatus.Ambiguous) => PluginErrorCodes.ProjectAmbiguous,
            ("Project", SelectorResolveStatus.Invalid) => PluginErrorCodes.ProjectSelectorInvalid,
            ("Project", _) => PluginErrorCodes.ProjectNotFound,
            ("Symbol", SelectorResolveStatus.Ambiguous) => PluginErrorCodes.SymbolAmbiguous,
            ("Symbol", SelectorResolveStatus.Invalid) => PluginErrorCodes.SymbolSelectorInvalid,
            ("Symbol", _) => PluginErrorCodes.SymbolNotFound,
            ("Location", SelectorResolveStatus.Ambiguous) => PluginErrorCodes.LocationAmbiguous,
            ("Location", SelectorResolveStatus.Invalid) => PluginErrorCodes.LocationSelectorInvalid,
            ("Location", _) => PluginErrorCodes.LocationNotFound,
            _ => throw new InvalidOperationException($"Selector target '{targetCode}' is not supported."),
        };
    }
}
