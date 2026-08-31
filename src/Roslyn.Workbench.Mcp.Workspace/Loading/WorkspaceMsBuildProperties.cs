using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Defines the allowlisted MSBuild global properties accepted for workspace evaluation.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WorkspaceMsBuildProperties
{
    /// <summary>
    /// Existing absolute directory used for MSBuild intermediate and output artifacts.
    /// </summary>
    [Description("Existing absolute directory used for MSBuild intermediate and output artifacts.")]
    public string? ArtifactsPath { get; init; }

    /// <summary>
    /// MSBuild Configuration global property, such as Debug or Release.
    /// </summary>
    [Description("MSBuild Configuration global property, such as Debug or Release.")]
    public string? Configuration { get; init; }

    /// <summary>
    /// MSBuild Platform global property, such as AnyCPU or x64.
    /// </summary>
    [Description("MSBuild Platform global property, such as AnyCPU or x64.")]
    public string? Platform { get; init; }

    /// <summary>
    /// MSBuild RuntimeIdentifier global property used to evaluate the workspace.
    /// </summary>
    [Description("MSBuild RuntimeIdentifier global property used to evaluate the workspace.")]
    public string? RuntimeIdentifier { get; init; }

    /// <summary>
    /// MSBuild TargetFramework global property used to select a target-specific project evaluation.
    /// </summary>
    [Description("MSBuild TargetFramework global property used to select a target-specific project evaluation.")]
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Converts the configured values to their MSBuild global-property names.
    /// </summary>
    /// <returns>A case-insensitive dictionary containing each configured property.</returns>
    public Dictionary<string, string> ToGlobalProperties()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddProperty(properties, nameof(ArtifactsPath), ArtifactsPath);
        AddProperty(properties, nameof(Configuration), Configuration);
        AddProperty(properties, nameof(Platform), Platform);
        AddProperty(properties, nameof(RuntimeIdentifier), RuntimeIdentifier);
        AddProperty(properties, nameof(TargetFramework), TargetFramework);
        return properties;
    }

    private static void AddProperty(Dictionary<string, string> properties, string name, string? value)
    {
        if (value is not null)
        {
            properties.Add(name, value);
        }
    }
}
