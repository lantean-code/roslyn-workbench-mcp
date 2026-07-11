using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

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

        var errorSchema = CreateValueSchema<Protocol.Results.ToolError>();
        var nextSchema = CreateValueSchema<Workspace.Contracts.Results.RequiredAction>();
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
        var schemaObject = ParseObject(schema);

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
        var valueObject = ParseObject(valueSchema);
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
                if (requiredProperty is not null)
                {
                    successRequired.Add(requiredProperty.DeepClone());
                }
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
        var method = GetRequiredMethod(typeof(SchemaProbe<TRequest>), nameof(SchemaProbe<TRequest>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");

        return CloneSchemaNode(requestSchema, root);
    }

    private static JsonElement CreateValueSchemaCore(Type valueType)
    {
        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(BoundedCollection<>))
        {
            return CreateBoundedCollectionSchema(valueType.GetGenericArguments()[0]);
        }

        var method = GetRequiredMethod(typeof(ToolSchemaBuilder), nameof(CreateValueSchemaCoreGeneric), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(valueType);

        return method.Invoke(null, null) is JsonElement schema
            ? schema
            : throw new InvalidOperationException("Schema generation did not return a JSON element.");
    }

    private static JsonElement CreateValueSchemaCoreGeneric<TValue>()
    {
        var method = GetRequiredMethod(typeof(SchemaValueProbe<TValue>), nameof(SchemaValueProbe<TValue>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");
        var valueSchema = requestSchema.GetProperty("properties").GetProperty("value");

        return CloneSchemaNode(valueSchema, root);
    }

    private static JsonElement CloneSchemaNode(JsonElement schemaNode, JsonElement root)
    {
        var schemaObject = ParseObject(schemaNode);

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

            foreach (var definition in ParseObject(childDefinitions))
            {
                definitions[definition.Key] = definition.Value?.DeepClone();
            }
        }

        return definitions;
    }

    private static MethodInfo GetRequiredMethod(Type type, string name, BindingFlags bindingFlags)
    {
        return type.GetMethod(name, bindingFlags)
            ?? throw new InvalidOperationException($"Schema method '{type.FullName}.{name}' was not found.");
    }

    private static JsonObject ParseObject(JsonElement element)
    {
        return JsonNode.Parse(element.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Generated schema was not a JSON object.");
    }

    private static JsonElement CreateBoundedCollectionSchema(Type itemType)
    {
        var itemSchema = CreateValueSchema(itemType);
        var definitions = MergeDefinitions([itemSchema]);
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("items", "hasMore"),
            ["properties"] = new JsonObject
            {
                ["items"] = CreateArraySchema(itemSchema),
                ["hasMore"] = new JsonObject
                {
                    ["type"] = "boolean",
                },
            },
        };

        if (definitions.Count > 0)
        {
            schema["$defs"] = definitions;
        }

        return JsonSerializer.SerializeToElement(schema);
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
