using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolSchemaBuilder
{
    public static JsonElement CreateDirectOutputSchema(
        JsonElement valueSchema,
        JsonElement errorSchema,
        JsonElement nextSchema)
    {
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
            [valueSchema],
            errorSchema,
            nextSchema);
    }

    public static JsonElement CreateResponseSchema(
        JsonObject successSchema,
        IReadOnlyList<JsonElement> componentSchemas,
        JsonElement errorSchema,
        JsonElement nextSchema)
    {
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

    public static JsonElement CreateBoundedCollectionSchema(JsonElement itemSchema)
    {
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

    public static JsonElement NormalizeExportedSchema(JsonElement schemaNode, JsonElement root)
    {
        var schemaObject = ParseObject(schemaNode);

        if (schemaObject["type"] is JsonArray typeArray
            && typeArray.Any(static node => string.Equals(node?.GetValue<string>(), "object", StringComparison.Ordinal)))
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

    private static JsonObject ParseObject(JsonElement element)
    {
        return JsonNode.Parse(element.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Generated schema was not a JSON object.");
    }
}
