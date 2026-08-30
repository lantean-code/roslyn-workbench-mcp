using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ContractDescriptionSchemaTransformer
{
    public static JsonNode Transform(AIJsonSchemaCreateContext context, JsonNode schema)
    {
        if (schema is not JsonObject schemaObject || context.TypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return schema;
        }

        PublishTypeDescription(schemaObject, context.TypeInfo);
        PublishPropertyDescriptions(schemaObject, context.TypeInfo);
        return schemaObject;
    }

    public static void PublishTypeDescription(JsonObject schema, JsonTypeInfo contractTypeInfo)
    {
        var description = contractTypeInfo.Type.GetCustomAttribute<DescriptionAttribute>(inherit: true);
        if (description is not null)
        {
            schema["description"] = description.Description;
        }
    }

    public static void PublishPropertyDescriptions(JsonObject schema, JsonTypeInfo contractTypeInfo)
    {
        foreach (var property in contractTypeInfo.Properties)
        {
            PublishPropertyDescription(schema, property);
        }
    }

    public static bool TryGetPropertySchema(
        JsonObject schema,
        JsonPropertyInfo property,
        [NotNullWhen(true)] out JsonObject? propertySchema)
    {
        propertySchema = null;
        if (schema["properties"] is not JsonObject schemaProperties)
        {
            return false;
        }

        propertySchema = schemaProperties[property.Name] as JsonObject;
        return propertySchema is not null;
    }

    private static void PublishPropertyDescription(JsonObject schema, JsonPropertyInfo property)
    {
        if (property.AttributeProvider is null
            || !TryGetPropertySchema(schema, property, out var propertySchema))
        {
            return;
        }

        var attributes = property.AttributeProvider.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true);
        if (attributes.Length == 0)
        {
            return;
        }

        var description = (DescriptionAttribute)attributes[0];
        propertySchema["description"] = description.Description;
    }
}
