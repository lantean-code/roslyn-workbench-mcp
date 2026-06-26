using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Server;

namespace Roslyn.Workbench.Mcp.Plugins;

public static class ToolSchemaFactory
{
    public static JsonElement CreateInputSchema<TRequest>()
    {
        return CreateInputSchemaCore<TRequest>();
    }

    public static JsonElement CreateToolResultSchema<TResult>()
    {
        var dataSchema = CreateValueSchema<TResult>();
        var changeSchema = CreateValueSchema<Contracts.Results.ChangeSummary>();
        var errorSchema = CreateValueSchema<Contracts.Results.ToolError>();
        var requiredActionSchema = CreateValueSchema<Contracts.Results.RequiredAction>();
        var diagnosticSchema = CreateValueSchema<Contracts.Results.DiagnosticInfo>();
        var warningSchema = CreateValueSchema<Contracts.Results.WarningInfo>();

        var oneOf = new JsonArray
        {
            CreateSuccessVariant("Succeeded", dataSchema, changeSchema, diagnosticSchema, warningSchema, requiredActionSchema),
            CreateNoChangeVariant(dataSchema, diagnosticSchema, warningSchema),
            CreateErrorVariant("Rejected", errorSchema, diagnosticSchema, warningSchema, requiredActionSchema),
            CreateErrorVariant("Conflict", errorSchema, diagnosticSchema, warningSchema, requiredActionSchema),
            CreateErrorVariant("Faulted", errorSchema, diagnosticSchema, warningSchema, requiredActionSchema),
        };

        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["oneOf"] = oneOf,
        });
    }

    private static JsonElement CreateInputSchemaCore<TRequest>()
    {
        var method = typeof(SchemaProbe<TRequest>).GetMethod(nameof(SchemaProbe<>.Invoke), BindingFlags.Public | BindingFlags.Static)!;
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");

        return CloneSchemaNode(requestSchema, root);
    }

    private static JsonElement CreateValueSchema<TValue>()
    {
        var method = typeof(SchemaValueProbe<TValue>).GetMethod(nameof(SchemaValueProbe<>.Invoke), BindingFlags.Public | BindingFlags.Static)!;
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

    private static JsonObject CreateSuccessVariant(
        string outcome,
        JsonElement dataSchema,
        JsonElement changeSchema,
        JsonElement diagnosticSchema,
        JsonElement warningSchema,
        JsonElement requiredActionSchema)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("outcome", "data", "diagnostics", "warnings"),
            ["properties"] = CreateCommonProperties(outcome, diagnosticSchema, warningSchema, requiredActionSchema, dataSchema, changeSchema),
        };
    }

    private static JsonObject CreateNoChangeVariant(JsonElement dataSchema, JsonElement diagnosticSchema, JsonElement warningSchema)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("outcome", "diagnostics", "warnings"),
            ["properties"] = new JsonObject
            {
                ["outcome"] = new JsonObject
                {
                    ["const"] = "NoChange",
                },
                ["workspaceEpoch"] = CreateNullablePrimitiveSchema("integer"),
                ["transactionRevision"] = CreateNullablePrimitiveSchema("integer"),
                ["data"] = AllowNull(dataSchema),
                ["diagnostics"] = CreateArraySchema(diagnosticSchema),
                ["warnings"] = CreateArraySchema(warningSchema),
            },
        };
    }

    private static JsonObject CreateErrorVariant(
        string outcome,
        JsonElement errorSchema,
        JsonElement diagnosticSchema,
        JsonElement warningSchema,
        JsonElement requiredActionSchema)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["required"] = new JsonArray("outcome", "error", "diagnostics", "warnings"),
            ["properties"] = new JsonObject
            {
                ["outcome"] = new JsonObject
                {
                    ["const"] = outcome,
                },
                ["workspaceEpoch"] = CreateNullablePrimitiveSchema("integer"),
                ["transactionRevision"] = CreateNullablePrimitiveSchema("integer"),
                ["diagnostics"] = CreateArraySchema(diagnosticSchema),
                ["warnings"] = CreateArraySchema(warningSchema),
                ["error"] = JsonNode.Parse(errorSchema.GetRawText()),
                ["requiredAction"] = AllowNull(requiredActionSchema),
            },
        };
    }

    private static JsonObject CreateCommonProperties(
        string outcome,
        JsonElement diagnosticSchema,
        JsonElement warningSchema,
        JsonElement requiredActionSchema,
        JsonElement dataSchema,
        JsonElement changeSchema)
    {
        return new JsonObject
        {
            ["outcome"] = new JsonObject
            {
                ["const"] = outcome,
            },
            ["workspaceEpoch"] = CreateNullablePrimitiveSchema("integer"),
            ["transactionRevision"] = CreateNullablePrimitiveSchema("integer"),
            ["data"] = JsonNode.Parse(dataSchema.GetRawText()),
            ["changes"] = AllowNull(changeSchema),
            ["diagnostics"] = CreateArraySchema(diagnosticSchema),
            ["warnings"] = CreateArraySchema(warningSchema),
            ["requiredAction"] = AllowNull(requiredActionSchema),
        };
    }

    private static JsonObject CreateArraySchema(JsonElement itemSchema)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = JsonNode.Parse(itemSchema.GetRawText()),
        };
    }

    private static JsonNode AllowNull(JsonElement schema)
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

    private static JsonObject CreateNullablePrimitiveSchema(string type)
    {
        return new JsonObject
        {
            ["type"] = new JsonArray(type, "null"),
        };
    }

    [McpServerTool(Name = "schema-input-probe")]
    private static string CreateInputSchemaProbe<TRequest>(TRequest request)
    {
        _ = request;

        return string.Empty;
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

    private sealed record SchemaValueWrapper<TValue>
    {
        public TValue? Value { get; init; }
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
