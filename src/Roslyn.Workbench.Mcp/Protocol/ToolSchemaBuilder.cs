using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolSchemaBuilder
{
    private const string ItemsDescription = "Items returned in this page.";
    private const string HasMoreDescription = "Whether additional items were available beyond this page.";
    private const string TotalCountDescription = "Complete result count, when available without additional expensive work.";
    private const string OkDescription = "Whether the tool invocation succeeded.";
    private const string DataDescription = "Tool-specific result payload.";
    private const string SnapshotDescription = "Exact immutable workspace snapshot associated with the result, when available.";
    private const string ErrorDescription = "Structured error details when the invocation failed.";
    private const string ContinuationDescription = "Action the agent should take before retrying or continuing.";

    public static JsonElement CreateDirectOutputSchema(
        JsonElement valueSchema,
        JsonElement errorSchema,
        JsonElement continuationSchema,
        JsonElement snapshotSchema)
    {
        var successSchema = CreateNullableSuccessSchema(
            valueSchema,
            snapshotSchema,
            snapshotRequired: false);

        return CreateResponseSchema(
            successSchema,
            [valueSchema, snapshotSchema],
            errorSchema,
            continuationSchema);
    }

    public static JsonElement CreateResponseSchema(
        JsonObject successSchema,
        IReadOnlyList<JsonElement> componentSchemas,
        JsonElement errorSchema,
        JsonElement continuationSchema)
    {
        var mergedDefinitions = MergeDefinitions(componentSchemas.Concat([errorSchema, continuationSchema]));
        var failureSchema = CreateFailureSchema(errorSchema, continuationSchema);
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
            ["description"] = HasMoreDescription,
        };

        var totalCountSchema = new JsonObject
        {
            ["type"] = "integer",
            ["minimum"] = 0,
            ["description"] = TotalCountDescription,
        };

        var properties = new JsonObject
        {
            ["items"] = CreateArraySchema(itemSchema, ItemsDescription),
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

    public static JsonObject CreateArraySchema(JsonElement itemSchema, string? description = null)
    {
        var parsedItemSchema = ParseNode(itemSchema);
        var schema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = parsedItemSchema,
        };

        if (description is not null)
        {
            schema["description"] = description;
        }

        return schema;
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

    public static JsonObject CreateNullableSuccessSchema(
        JsonElement dataSchema,
        JsonElement snapshotSchema,
        bool snapshotRequired)
    {
        return CreateSuccessSchema(
            AllowNull(dataSchema),
            snapshotSchema,
            snapshotRequired);
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

    public static JsonObject CreateSuccessSchema(
        JsonNode? dataSchema,
        JsonElement snapshotSchema,
        bool snapshotRequired)
    {
        var okSchema = new JsonObject
        {
            ["const"] = true,
            ["description"] = OkDescription,
        };

        var properties = new JsonObject
        {
            ["ok"] = okSchema,
            ["data"] = AddDescription(dataSchema, DataDescription),
            ["snapshot"] = AddDescription(ParseNode(snapshotSchema), SnapshotDescription),
        };

        var requiredProperties = new JsonArray("ok", "data");
        if (snapshotRequired)
        {
            requiredProperties.Add("snapshot");
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredProperties,
            ["properties"] = properties,
        };
    }

    private static JsonObject CreateFailureSchema(JsonElement errorSchema, JsonElement continuationSchema)
    {
        var okSchema = new JsonObject
        {
            ["const"] = false,
            ["description"] = OkDescription,
        };

        var properties = new JsonObject
        {
            ["ok"] = okSchema,
            ["error"] = AddDescription(ParseNode(errorSchema), ErrorDescription),
            ["continuation"] = AddDescription(ParseNode(continuationSchema), ContinuationDescription),
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

    private static JsonNode? AddDescription(JsonNode? schema, string description)
    {
        if (schema is JsonObject schemaObject)
        {
            schemaObject["description"] = description;
        }

        return schema;
    }

    private static JsonNode? ParseNode(JsonElement element)
    {
        return JsonNode.Parse(element.GetRawText());
    }
}
