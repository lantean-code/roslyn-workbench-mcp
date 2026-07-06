using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Tooling;

internal static class ToolSchemaBuilder
{
    private static readonly ConcurrentDictionary<Type, JsonElement> _directOutputSchemaCache = [];
    private static readonly ConcurrentDictionary<Type, JsonElement> _inputSchemaCache = [];
    private static readonly ConcurrentDictionary<Type, JsonElement> _valueSchemaCache = [];

    public static JsonElement CreateInputSchema<TRequest>()
    {
        return _inputSchemaCache.GetOrAdd(typeof(TRequest), static _ => CreateInputSchemaCore<TRequest>());
    }

    public static JsonElement CreateDirectOutputSchema(Type responseType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        return _directOutputSchemaCache.GetOrAdd(responseType, static type => CreateDirectOutputSchemaCore(type));
    }

    public static JsonElement CreateValueSchema<TValue>()
    {
        return CreateValueSchema(typeof(TValue));
    }

    public static JsonElement CreateValueSchema(Type valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);

        return _valueSchemaCache.GetOrAdd(valueType, static type => CreateValueSchemaCore(type));
    }

    public static JsonElement CreateResponseSchema(JsonObject successSchema, IReadOnlyList<JsonElement> componentSchemas)
    {
        ArgumentNullException.ThrowIfNull(successSchema);
        ArgumentNullException.ThrowIfNull(componentSchemas);

        var errorSchema = CreateValueSchema<Contracts.Results.ToolError>();
        var nextSchema = CreateValueSchema<Contracts.Results.RequiredAction>();
        var mergedDefinitions = MergeDefinitions(componentSchemas.Concat([errorSchema, nextSchema]));
        var root = new JsonObject
        {
            ["type"] = "object",
            ["oneOf"] = new JsonArray
            {
                successSchema,
                new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = new JsonArray("ok", "error"),
                    ["properties"] = new JsonObject
                    {
                        ["ok"] = new JsonObject
                        {
                            ["const"] = false,
                        },
                        ["error"] = JsonNode.Parse(errorSchema.GetRawText()),
                        ["next"] = AllowNull(nextSchema),
                    },
                },
            },
        };

        if (mergedDefinitions.Count > 0)
        {
            root["$defs"] = mergedDefinitions;
        }

        return JsonSerializer.SerializeToElement(root);
    }

    public static JsonObject CreateArraySchema(JsonElement itemSchema)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = JsonNode.Parse(itemSchema.GetRawText()),
        };
    }

    public static JsonNode AllowNull(JsonElement schema)
    {
        var schemaObject = JsonNode.Parse(schema.GetRawText())!.AsObject();

        if (schemaObject["type"] is JsonValue typeValue)
        {
            schemaObject["type"] = new JsonArray(typeValue.GetValue<string>(), "null");
            return schemaObject;
        }

        if (schemaObject["type"] is JsonArray typeArray)
        {
            if (!typeArray.Any(static node => string.Equals(node?.GetValue<string>(), "null", StringComparison.Ordinal)))
            {
                typeArray.Add("null");
            }

            return schemaObject;
        }

        return new JsonObject
        {
            ["anyOf"] = new JsonArray
            {
                schemaObject,
                new JsonObject
                {
                    ["type"] = "null",
                },
            },
        };
    }

    public static JsonObject CreateNullablePrimitiveSchema(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return new JsonObject
        {
            ["type"] = new JsonArray(type, "null"),
        };
    }

    private static JsonElement CreateDirectOutputSchemaCore(Type responseType)
    {
        var valueSchema = CreateValueSchema(responseType);
        var valueObject = JsonNode.Parse(valueSchema.GetRawText())!.AsObject();
        var successProperties = new JsonObject
        {
            ["ok"] = new JsonObject
            {
                ["const"] = true,
            },
        };
        var successRequired = new JsonArray("ok");

        if (valueObject["properties"] is JsonObject valueProperties)
        {
            foreach (var property in valueProperties)
            {
                successProperties[property.Key] = property.Value?.DeepClone();
            }
        }

        if (valueObject["required"] is JsonArray requiredProperties)
        {
            foreach (var requiredProperty in requiredProperties)
            {
                successRequired.Add(requiredProperty!.DeepClone());
            }
        }

        return CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = successRequired,
                ["properties"] = successProperties,
            },
            [valueSchema]);
    }

    private static JsonElement CreateInputSchemaCore<TRequest>()
    {
        var method = typeof(SchemaProbe<TRequest>).GetMethod(nameof(SchemaProbe<TRequest>.Invoke), BindingFlags.Public | BindingFlags.Static)!;
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");

        return CloneSchemaNode(requestSchema, root);
    }

    private static JsonElement CreateValueSchemaCore(Type valueType)
    {
        var method = typeof(ToolSchemaBuilder)
            .GetMethod(nameof(CreateValueSchemaCoreGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueType);

        return (JsonElement)method.Invoke(null, null)!;
    }

    private static JsonElement CreateValueSchemaCoreGeneric<TValue>()
    {
        var method = typeof(SchemaValueProbe<TValue>).GetMethod(nameof(SchemaValueProbe<TValue>.Invoke), BindingFlags.Public | BindingFlags.Static)!;
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");
        var valueSchema = requestSchema.GetProperty("properties").GetProperty("value");

        return CloneSchemaNode(valueSchema, root);
    }

    private static JsonElement CloneSchemaNode(JsonElement schemaNode, JsonElement root)
    {
        var schemaObject = JsonNode.Parse(schemaNode.GetRawText())!.AsObject();

        if (schemaObject["type"] is JsonArray typeArray && typeArray.Any(static node => string.Equals(node?.GetValue<string>(), "object", StringComparison.Ordinal)))
        {
            schemaObject["type"] = "object";
        }

        if (root.TryGetProperty("$defs", out var definitions))
        {
            schemaObject["$defs"] = JsonNode.Parse(definitions.GetRawText());
        }

        return JsonSerializer.SerializeToElement(schemaObject);
    }

    private static JsonObject MergeDefinitions(IEnumerable<JsonElement> schemas)
    {
        var definitions = new JsonObject();

        foreach (var schema in schemas)
        {
            if (!schema.TryGetProperty("$defs", out var childDefinitions))
            {
                continue;
            }

            foreach (var definition in JsonNode.Parse(childDefinitions.GetRawText())!.AsObject())
            {
                definitions[definition.Key] = definition.Value?.DeepClone();
            }
        }

        return definitions;
    }

    private sealed record SchemaValueWrapper<TValue>
    {
        public TValue? Value { get; init; }
    }

    private static class SchemaProbe<TRequest>
    {
        [McpServerTool(Name = "schema-input-probe")]
        public static string Invoke(TRequest request)
        {
            _ = request;

            return string.Empty;
        }
    }

    private static class SchemaValueProbe<TValue>
    {
        [McpServerTool(Name = "schema-value-probe")]
        public static string Invoke(SchemaValueWrapper<TValue> request)
        {
            _ = request;

            return string.Empty;
        }
    }
}
