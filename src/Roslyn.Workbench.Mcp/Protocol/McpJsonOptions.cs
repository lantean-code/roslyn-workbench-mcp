using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Provides the shared JSON settings used at MCP transport boundaries.
/// </summary>
internal static class McpJsonOptions
{
    /// <summary>
    /// Gets the settings used to generate transport schemas.
    /// </summary>
    public static JsonSerializerOptions Schema { get; } = Create(
        respectNullableAnnotations: true,
        unmappedMemberHandling: JsonUnmappedMemberHandling.Skip);

    /// <summary>
    /// Gets strict settings for binding incoming tool arguments.
    /// </summary>
    public static JsonSerializerOptions RequestBinding { get; } = Create(
        respectNullableAnnotations: true,
        unmappedMemberHandling: JsonUnmappedMemberHandling.Disallow);

    /// <summary>
    /// Gets the settings used to serialize published tool results.
    /// </summary>
    public static JsonSerializerOptions Results { get; } = Create(
        respectNullableAnnotations: false,
        unmappedMemberHandling: JsonUnmappedMemberHandling.Skip);

    private static JsonSerializerOptions Create(
        bool respectNullableAnnotations,
        JsonUnmappedMemberHandling unmappedMemberHandling)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = respectNullableAnnotations,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            UnmappedMemberHandling = unmappedMemberHandling,
        };

        serializerOptions.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

        serializerOptions.MakeReadOnly();
        return serializerOptions;
    }
}
