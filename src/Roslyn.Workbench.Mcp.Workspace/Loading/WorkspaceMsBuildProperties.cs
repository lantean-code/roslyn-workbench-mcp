using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WorkspaceMsBuildProperties
{
    public string? ArtifactsPath { get; init; }

    public string? Configuration { get; init; }

    public string? Platform { get; init; }

    public string? RuntimeIdentifier { get; init; }

    public string? TargetFramework { get; init; }

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
