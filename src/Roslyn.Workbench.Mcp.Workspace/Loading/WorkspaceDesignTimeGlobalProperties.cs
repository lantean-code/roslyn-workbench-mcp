namespace Roslyn.Workbench.Mcp.Workspace.Loading;

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
