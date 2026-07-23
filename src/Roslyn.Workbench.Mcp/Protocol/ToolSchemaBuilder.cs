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
        var successSchema = CreateSuccessSchema(ParseNode(valueSchema));
        return CreateResponseSchema(successSchema, [valueSchema], errorSchema, nextSchema);
    }

    public static JsonElement CreateResponseSchema(
        JsonObject successSchema,
        IReadOnlyList<JsonElement> componentSchemas,
        JsonElement errorSchema,
        JsonElement nextSchema)
    {
        var mergedDefinitions = MergeDefinitions(componentSchemas.Concat([errorSchema, nextSchema]));
        var failureSchema = CreateFailureSchema(errorSchema, nextSchema);
        var alternatives = new JsonArray
        {
            successSchema,
            failureSchema,
        };

        var root = new JsonObject
        {
            ["type"] = "object",
            ["oneOf"] = alternatives,
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
        var hasMoreSchema = new JsonObject
        {
            ["type"] = "boolean",
        };

        var totalCountSchema = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = 0,
        };

        var properties = new JsonObject
        {
            ["items"] = CreateArraySchema(itemSchema),
            ["hasMore"] = hasMoreSchema,
            ["totalCount"] = totalCountSchema,
        };

        var requiredProperties = new JsonArray("items", "hasMore");
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredProperties,
            ["properties"] = properties,
        };

        if (definitions.Count > 0)
        {
            schema["$defs"] = definitions;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    public static JsonObject CreateArraySchema(JsonElement itemSchema)
    {
        var parsedItemSchema = ParseNode(itemSchema);
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = parsedItemSchema,
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

        var nullSchema = new JsonObject
        {
            ["type"] = "null",
        };

        var alternatives = new JsonArray
        {
            schemaObject,
            nullSchema,
        };

        return new JsonObject
        {
            ["anyOf"] = alternatives,
        };
    }

    public static JsonObject CreateNullablePrimitiveSchema(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var allowedTypes = new JsonArray(type, "null");
        return new JsonObject
        {
            ["type"] = allowedTypes,
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

    public static JsonObject CreateSuccessSchema(JsonNode? dataSchema)
    {
        var okSchema = new JsonObject
        {
            ["const"] = true,
        };

        var properties = new JsonObject
        {
            ["ok"] = okSchema,
            ["data"] = dataSchema,
        };

        var requiredProperties = new JsonArray("ok", "data");
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredProperties,
            ["properties"] = properties,
        };
    }

    private static JsonObject CreateFailureSchema(JsonElement errorSchema, JsonElement nextSchema)
    {
        var okSchema = new JsonObject
        {
            ["const"] = false,
        };

        var properties = new JsonObject
        {
            ["ok"] = okSchema,
            ["error"] = ParseNode(errorSchema),
            ["next"] = AllowNull(nextSchema),
        };

        var requiredProperties = new JsonArray("ok", "error");
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredProperties,
            ["properties"] = properties,
        };
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
        var node = ParseNode(element);
        if (node is not JsonObject schemaObject)
        {
            throw new InvalidOperationException("Generated schema was not a JSON object.");
        }

        return schemaObject;
    }

    private static JsonNode? ParseNode(JsonElement element)
    {
        return JsonNode.Parse(element.GetRawText());
    }
}
