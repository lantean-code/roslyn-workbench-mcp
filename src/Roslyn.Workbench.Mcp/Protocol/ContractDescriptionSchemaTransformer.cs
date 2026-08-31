using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Publishes contract-level <see cref="DescriptionAttribute"/> text in generated JSON schemas.
/// </summary>
internal static class ContractDescriptionSchemaTransformer
{
    /// <summary>
    /// Adds type and property descriptions from contract attributes to an object schema.
    /// </summary>
    /// <param name="context">The schema-generation context for the contract type.</param>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <returns>The supplied schema with applicable contract descriptions added.</returns>
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

    /// <summary>
    /// Adds the contract type's description to its schema when one is declared.
    /// </summary>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <param name="contractTypeInfo">The JSON contract metadata whose descriptions are published.</param>
    public static void PublishTypeDescription(JsonObject schema, JsonTypeInfo contractTypeInfo)
    {
        var description = contractTypeInfo.Type.GetCustomAttribute<DescriptionAttribute>(inherit: true);
        if (description is not null)
        {
            schema["description"] = description.Description;
        }
    }

    /// <summary>
    /// Adds declared property descriptions to their corresponding schema properties.
    /// </summary>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <param name="contractTypeInfo">The JSON contract metadata whose descriptions are published.</param>
    public static void PublishPropertyDescriptions(JsonObject schema, JsonTypeInfo contractTypeInfo)
    {
        foreach (var property in contractTypeInfo.Properties)
        {
            PublishPropertyDescription(schema, property);
        }
    }

    /// <summary>
    /// Attempts to find the schema generated for a serialized property.
    /// </summary>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <param name="property">The JSON property metadata whose schema is required.</param>
    /// <param name="propertySchema">The schema generated for the JSON property.</param>
    /// <returns><see langword="true"/> when the schema contains an object entry for the property; otherwise, <see langword="false"/>.</returns>
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
