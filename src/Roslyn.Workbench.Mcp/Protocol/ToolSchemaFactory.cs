using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolSchemaFactory
{
    public static JsonElement CreateInputSchema<TRequest>()
    {
        return ToolSchemaBuilder.CreateInputSchema<TRequest>();
    }

    public static JsonElement CreateOutputSchema(PublishedToolKind kind, Type responseType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        if (kind == PublishedToolKind.Mutation)
        {
            return CreateMutationResponseSchema();
        }

        return CreateQueryResponseSchema(responseType);
    }

    public static JsonElement CreateToolResultSchema<TResult>()
    {
        var dataSchema = ToolSchemaBuilder.CreateValueSchema<TResult>();
        var changeSchema = ToolSchemaBuilder.CreateValueSchema<Workspace.Contracts.Results.ChangeSummary>();
        var errorSchema = ToolSchemaBuilder.CreateValueSchema<Protocol.Results.ToolError>();
        var requiredActionSchema = ToolSchemaBuilder.CreateValueSchema<Workspace.Contracts.Results.RequiredAction>();
        var diagnosticSchema = ToolSchemaBuilder.CreateValueSchema<Workspace.Contracts.Results.DiagnosticInfo>();
        var warningSchema = ToolSchemaBuilder.CreateValueSchema<Workspace.Contracts.Results.WarningInfo>();

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

    private static JsonElement CreateQueryResponseSchema(Type responseType)
    {
        var valueSchema = ToolSchemaBuilder.CreateValueSchema(responseType);

        return ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("ok", "data"),
                ["properties"] = new JsonObject
                {
                    ["ok"] = new JsonObject
                    {
                        ["const"] = true,
                    },
                    ["data"] = JsonNode.Parse(valueSchema.GetRawText()),
                },
            },
            [valueSchema]);
    }

    private static JsonElement CreateMutationResponseSchema()
    {
        return ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("ok", "staged"),
                ["properties"] = new JsonObject
                {
                    ["ok"] = new JsonObject
                    {
                        ["const"] = true,
                    },
                    ["staged"] = new JsonObject
                    {
                        ["type"] = "boolean",
                    },
                    ["summary"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("string"),
                    ["transaction"] = new JsonObject
                    {
                        ["type"] = new JsonArray("object", "null"),
                        ["required"] = new JsonArray("revision"),
                        ["properties"] = new JsonObject
                        {
                            ["revision"] = new JsonObject
                            {
                                ["type"] = "integer",
                            },
                        },
                    },
                },
            },
            []);
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
                ["workspaceEpoch"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
                ["transactionRevision"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
                ["data"] = ToolSchemaBuilder.AllowNull(dataSchema),
                ["diagnostics"] = ToolSchemaBuilder.CreateArraySchema(diagnosticSchema),
                ["warnings"] = ToolSchemaBuilder.CreateArraySchema(warningSchema),
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
                ["workspaceEpoch"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
                ["transactionRevision"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
                ["diagnostics"] = ToolSchemaBuilder.CreateArraySchema(diagnosticSchema),
                ["warnings"] = ToolSchemaBuilder.CreateArraySchema(warningSchema),
                ["error"] = JsonNode.Parse(errorSchema.GetRawText()),
                ["requiredAction"] = ToolSchemaBuilder.AllowNull(requiredActionSchema),
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
            ["workspaceEpoch"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
            ["transactionRevision"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("integer"),
            ["data"] = JsonNode.Parse(dataSchema.GetRawText()),
            ["changes"] = ToolSchemaBuilder.AllowNull(changeSchema),
            ["diagnostics"] = ToolSchemaBuilder.CreateArraySchema(diagnosticSchema),
            ["warnings"] = ToolSchemaBuilder.CreateArraySchema(warningSchema),
            ["requiredAction"] = ToolSchemaBuilder.AllowNull(requiredActionSchema),
        };
    }
}
