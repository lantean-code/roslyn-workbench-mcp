using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class InputSchemaContractPublisher
{
    public static JsonElement Publish(JsonElement schema, Type requestType)
    {
        var parsedSchema = JsonNode.Parse(schema.GetRawText());
        if (parsedSchema is not JsonObject root)
        {
            throw new InvalidOperationException("Generated input schema was not a JSON object.");
        }

        var visited = new HashSet<(Type Type, JsonObject Schema)>(SchemaVisitComparer.Instance);
        var nullabilityContext = new NullabilityInfoContext();

        PublishContractMetadata(root, root, requestType, nullabilityContext, visited);

        return JsonSerializer.SerializeToElement(root);
    }

    private static void PublishContractMetadata(
        JsonObject root,
        JsonObject schema,
        Type contractType,
        NullabilityInfoContext nullabilityContext,
        HashSet<(Type Type, JsonObject Schema)> visited)
    {
        contractType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (!visited.Add((contractType, schema)) || schema["properties"] is not JsonObject schemaProperties)
        {
            return;
        }

        foreach (var property in contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            jsonPropertyName ??= JsonNamingPolicy.CamelCase.ConvertName(property.Name);

            if (schemaProperties[jsonPropertyName] is not JsonObject propertySchema)
            {
                continue;
            }

            var nullability = nullabilityContext.Create(property);
            if (nullability.WriteState == NullabilityState.NotNull)
            {
                RemoveNullType(propertySchema);
            }

            var declaredDefault = property.GetCustomAttribute<DefaultValueAttribute>();
            if (declaredDefault is not null)
            {
                propertySchema["default"] = JsonSerializer.SerializeToNode(declaredDefault.Value);
            }

            foreach (var nestedSchema in ResolveNestedSchemas(root, propertySchema))
            {
                PublishContractMetadata(root, nestedSchema, property.PropertyType, nullabilityContext, visited);
            }
        }
    }

    private static void RemoveNullType(JsonObject schema)
    {
        if (schema["type"] is not JsonArray types)
        {
            return;
        }

        for (var index = types.Count - 1; index >= 0; index--)
        {
            if (types[index] is JsonValue type
                && type.TryGetValue<string>(out var typeName)
                && string.Equals(typeName, "null", StringComparison.Ordinal))
            {
                types.RemoveAt(index);
            }
        }
    }

    private static IEnumerable<JsonObject> ResolveNestedSchemas(JsonObject root, JsonObject schema)
    {
        if (ResolveReference(root, schema) is JsonObject referencedSchema)
        {
            yield return referencedSchema;
        }

        if (schema["anyOf"] is not JsonArray alternatives)
        {
            yield break;
        }

        foreach (var alternative in alternatives.OfType<JsonObject>())
        {
            yield return ResolveReference(root, alternative) ?? alternative;
        }
    }

    private static JsonObject? ResolveReference(JsonObject root, JsonObject schema)
    {
        if (schema["$ref"] is not JsonValue referenceValue
            || !referenceValue.TryGetValue<string>(out var reference)
            || !reference.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            return null;
        }

        var definitionName = reference["#/$defs/".Length..].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        return root["$defs"]?[definitionName] as JsonObject;
    }

    private sealed class SchemaVisitComparer : IEqualityComparer<(Type Type, JsonObject Schema)>
    {
        public static SchemaVisitComparer Instance { get; } = new();

        public bool Equals((Type Type, JsonObject Schema) x, (Type Type, JsonObject Schema) y)
        {
            return x.Type == y.Type && ReferenceEquals(x.Schema, y.Schema);
        }

        public int GetHashCode((Type Type, JsonObject Schema) item)
        {
            return HashCode.Combine(item.Type, RuntimeHelpers.GetHashCode(item.Schema));
        }
    }
}
