using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class McpJsonOptions
{
    public static JsonSerializerOptions Schema { get; } = Create(
        respectNullableAnnotations: true,
        unmappedMemberHandling: JsonUnmappedMemberHandling.Skip);

    public static JsonSerializerOptions RequestBinding { get; } = Create(
        respectNullableAnnotations: true,
        unmappedMemberHandling: JsonUnmappedMemberHandling.Disallow);

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
