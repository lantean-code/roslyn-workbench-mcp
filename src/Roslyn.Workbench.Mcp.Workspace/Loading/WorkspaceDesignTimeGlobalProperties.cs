namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Builds the MSBuild global-property set required for design-time Roslyn workspace evaluation.
/// </summary>
internal static class WorkspaceDesignTimeGlobalProperties
{
    private static readonly IReadOnlyDictionary<string, string> _defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DesignTimeBuild"] = bool.TrueString,
        ["NonExistentFile"] = "__NonExistentSubDir__\\__NonExistentFile__",
        ["BuildProjectReferences"] = bool.FalseString,
        ["BuildingProject"] = bool.FalseString,
        ["ProvideCommandLineArgs"] = bool.TrueString,
        ["SkipCompilerExecution"] = bool.TrueString,
        ["ContinueOnError"] = "ErrorAndContinue",
        ["ShouldUnsetParentConfigurationAndPlatform"] = bool.FalseString,
    };

    /// <summary>
    /// Combines the design-time defaults with caller-supplied global properties.
    /// </summary>
    /// <param name="globalProperties">The optional caller-supplied properties, which override defaults with matching names.</param>
    /// <returns>A mutable property set for workspace construction.</returns>
    public static Dictionary<string, string> Create(IReadOnlyDictionary<string, string>? globalProperties)
    {
        var effectiveGlobalProperties = new Dictionary<string, string>(_defaults, StringComparer.OrdinalIgnoreCase);
        if (globalProperties is null)
        {
            return effectiveGlobalProperties;
        }

        foreach (var property in globalProperties)
        {
            effectiveGlobalProperties[property.Key] = property.Value;
        }

        return effectiveGlobalProperties;
    }
}
