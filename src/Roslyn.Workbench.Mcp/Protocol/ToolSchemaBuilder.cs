using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Composes the standard JSON schema shapes used by published tool responses.
/// </summary>
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

    /// <summary>
    /// Creates a success-or-failure schema for a directly published response value.
    /// </summary>
    /// <param name="valueSchema">The schema of the tool's successful data value.</param>
    /// <param name="errorSchema">The schema used for structured tool errors.</param>
    /// <param name="continuationSchema">The schema used for client continuation instructions.</param>
    /// <param name="snapshotSchema">The schema used for the workspace snapshot portion of the response.</param>
    /// <returns>The complete output schema, including reusable definitions.</returns>
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

    /// <summary>
    /// Combines success and failure alternatives into a complete tool response schema.
    /// </summary>
    /// <param name="successSchema">The schema used for successful tool results.</param>
    /// <param name="componentSchemas">The reusable component schemas referenced by the response schema.</param>
    /// <param name="errorSchema">The schema used for structured tool errors.</param>
    /// <param name="continuationSchema">The schema used for client continuation instructions.</param>
    /// <returns>The response schema with merged reusable definitions.</returns>
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

    /// <summary>
    /// Creates the standard paged collection schema around an item contract.
    /// </summary>
    /// <param name="itemSchema">The schema of each item in the generated collection.</param>
    /// <returns>A schema containing items, truncation state, and an optional total count.</returns>
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

    /// <summary>
    /// Creates an array schema for the supplied item contract.
    /// </summary>
    /// <param name="itemSchema">The schema applied to each item in the generated array.</param>
    /// <param name="description">Optional text describing the collection as a whole.</param>
    /// <returns>The array schema.</returns>
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

    /// <summary>
    /// Adds <see langword="null"/> to the schema's allowed types.
    /// </summary>
    /// <param name="schema">The JSON schema being inspected or transformed.</param>
    /// <returns>A cloned schema that accepts both the original value and <see langword="null"/>.</returns>
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

    /// <summary>
    /// Creates a schema that accepts a named JSON primitive type or <see langword="null"/>.
    /// </summary>
    /// <param name="type">The JSON Schema primitive type name.</param>
    /// <returns>The nullable primitive schema.</returns>
    public static JsonObject CreateNullablePrimitiveSchema(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var allowedTypes = new JsonArray(type, "null");
        return new JsonObject
        {
            ["type"] = allowedTypes,
        };
    }

    /// <summary>
    /// Creates a successful result envelope whose data value may be <see langword="null"/>.
    /// </summary>
    /// <param name="dataSchema">The schema of the successful result data.</param>
    /// <param name="snapshotSchema">The schema used for the workspace snapshot portion of the response.</param>
    /// <param name="snapshotRequired">Whether successful results must include a workspace snapshot.</param>
    /// <returns>The successful response alternative.</returns>
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

    /// <summary>
    /// Normalizes an exported schema for MCP publication.
    /// </summary>
    /// <param name="schemaNode">The exported schema node to normalize for transport.</param>
    /// <param name="root">The original exported root containing any referenced definitions.</param>
    /// <returns>A self-contained schema with local references normalized for publication.</returns>
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

    /// <summary>
    /// Creates a successful result envelope for a supplied data contract.
    /// </summary>
    /// <param name="dataSchema">The schema of the successful result data.</param>
    /// <param name="snapshotSchema">The schema used for the workspace snapshot portion of the response.</param>
    /// <param name="snapshotRequired">Whether successful results must include a workspace snapshot.</param>
    /// <returns>The successful response alternative.</returns>
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
