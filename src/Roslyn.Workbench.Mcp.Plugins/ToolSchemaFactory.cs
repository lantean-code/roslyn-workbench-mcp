using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.Tooling;

namespace Roslyn.Workbench.Mcp.Plugins;

public static class ToolSchemaFactory
{
    public static JsonElement CreateInputSchema<TRequest>()
    {
        return ToolSchemaBuilder.CreateInputSchema<TRequest>();
    }

    public static JsonElement CreateOutputSchema(ToolResponseDescriptor descriptor, Type responseType)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(responseType);

        return descriptor.Kind switch
        {
            ToolResponseShapeKind.Direct => ToolSchemaBuilder.CreateDirectOutputSchema(responseType),
            ToolResponseShapeKind.Singleton => CreateSingletonResponseSchema(responseType),
            ToolResponseShapeKind.Collection => CreateCollectionResponseSchema(responseType, descriptor.CollectionPropertyName!),
            ToolResponseShapeKind.Mutation => CreateMutationResponseSchema(),
            ToolResponseShapeKind.CodeActionList => CreateCodeActionListResponseSchema(),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{descriptor.Kind}'."),
        };
    }

    public static JsonElement CreateToolResultSchema<TResult>()
    {
        var dataSchema = ToolSchemaBuilder.CreateValueSchema<TResult>();
        var changeSchema = ToolSchemaBuilder.CreateValueSchema<Contracts.Results.ChangeSummary>();
        var errorSchema = ToolSchemaBuilder.CreateValueSchema<Contracts.Results.ToolError>();
        var requiredActionSchema = ToolSchemaBuilder.CreateValueSchema<Contracts.Results.RequiredAction>();
        var diagnosticSchema = ToolSchemaBuilder.CreateValueSchema<Contracts.Results.DiagnosticInfo>();
        var warningSchema = ToolSchemaBuilder.CreateValueSchema<Contracts.Results.WarningInfo>();

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

    private static JsonElement CreateSingletonResponseSchema(Type responseType)
    {
        var valueSchema = ToolSchemaBuilder.CreateValueSchema(responseType);

        return ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("ok", "value"),
                ["properties"] = new JsonObject
                {
                    ["ok"] = new JsonObject
                    {
                        ["const"] = true,
                    },
                    ["value"] = JsonNode.Parse(valueSchema.GetRawText()),
                },
            },
            [valueSchema]);
    }

    private static JsonElement CreateCollectionResponseSchema(Type responseType, string collectionPropertyName)
    {
        var responseSchema = ToolSchemaBuilder.CreateValueSchema(responseType);
        var collectionProperty = responseType.GetProperty(collectionPropertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Collection property '{collectionPropertyName}' was not found on '{responseType.FullName}'.");
        var itemType = collectionProperty.PropertyType.GetGenericArguments()[0];
        var itemSchema = ToolSchemaBuilder.CreateValueSchema(itemType);
        var extraSchemas = new List<JsonElement>
        {
            responseSchema,
            itemSchema,
        };
        var properties = new JsonObject
        {
            ["ok"] = new JsonObject
            {
                ["const"] = true,
            },
            ["items"] = ToolSchemaBuilder.CreateArraySchema(itemSchema),
            ["hasMore"] = new JsonObject
            {
                ["type"] = "boolean",
            },
        };

        foreach (var property in responseType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (string.Equals(property.Name, collectionPropertyName, StringComparison.Ordinal)
                || string.Equals(property.Name, "ReturnedCount", StringComparison.Ordinal)
                || string.Equals(property.Name, "HasMore", StringComparison.Ordinal))
            {
                continue;
            }

            var propertySchema = ToolSchemaBuilder.CreateValueSchema(property.PropertyType);
            extraSchemas.Add(propertySchema);
            properties[string.Equals(property.Name, "TruncationReasons", StringComparison.Ordinal)
                ? "truncatedBy"
                : JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = JsonNode.Parse(propertySchema.GetRawText());
        }

        return ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("ok", "items", "hasMore"),
                ["properties"] = properties,
            },
            extraSchemas);
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

    private static JsonElement CreateCodeActionListResponseSchema()
    {
        var itemSchema = ToolSchemaBuilder.CreateValueSchema(typeof(Contracts.CodeActions.CodeActionListItem));
        var truncationSchema = ToolSchemaBuilder.CreateValueSchema(typeof(IReadOnlyList<Contracts.Results.CollectionTruncation>));

        return ToolSchemaBuilder.CreateResponseSchema(
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("ok", "items", "hasMore"),
                ["properties"] = new JsonObject
                {
                    ["ok"] = new JsonObject
                    {
                        ["const"] = true,
                    },
                    ["items"] = ToolSchemaBuilder.CreateArraySchema(itemSchema),
                    ["hasMore"] = new JsonObject
                    {
                        ["type"] = "boolean",
                    },
                    ["truncatedBy"] = ToolSchemaBuilder.AllowNull(truncationSchema),
                },
            },
            [itemSchema, truncationSchema]);
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
