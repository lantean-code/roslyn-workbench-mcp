using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Plugins;

public static class ToolSchemaFactory
{
    public static JsonElement CreateInputSchema<TRequest>()
    {
        return CreateInputSchemaCore<TRequest>();
    }

    public static JsonElement CreateOutputSchema(ToolResponseDescriptor descriptor, Type responseType)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(responseType);

        return descriptor.Kind switch
        {
            ToolResponseShapeKind.Direct => CreateDirectResponseSchema(responseType),
            ToolResponseShapeKind.Singleton => CreateSingletonResponseSchema(responseType),
            ToolResponseShapeKind.Collection => CreateCollectionResponseSchema(responseType, descriptor.CollectionPropertyName!),
            ToolResponseShapeKind.Mutation => CreateMutationResponseSchema(),
            ToolResponseShapeKind.CodeActionList => CreateCodeActionListResponseSchema(),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{descriptor.Kind}'."),
        };
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
        return CreateValueSchema(typeof(TValue));
    }

    private static JsonElement CreateValueSchema(Type valueType)
    {
        var method = typeof(ToolSchemaFactory)
            .GetMethod(nameof(CreateValueSchemaCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueType);

        return (JsonElement)method.Invoke(null, null)!;
    }

    private static JsonElement CreateValueSchemaCore<TValue>()
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

    private static JsonElement CreateDirectResponseSchema(Type responseType)
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

    private static JsonElement CreateSingletonResponseSchema(Type responseType)
    {
        var valueSchema = CreateValueSchema(responseType);

        return CreateResponseSchema(
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
        var responseSchema = CreateValueSchema(responseType);
        var collectionProperty = responseType.GetProperty(collectionPropertyName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Collection property '{collectionPropertyName}' was not found on '{responseType.FullName}'.");
        var itemType = collectionProperty.PropertyType.GetGenericArguments()[0];
        var itemSchema = CreateValueSchema(itemType);
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
            ["items"] = CreateArraySchema(itemSchema),
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

            var propertySchema = CreateValueSchema(property.PropertyType);
            extraSchemas.Add(propertySchema);
            properties[string.Equals(property.Name, "TruncationReasons", StringComparison.Ordinal)
                ? "truncatedBy"
                : JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = JsonNode.Parse(propertySchema.GetRawText());
        }

        return CreateResponseSchema(
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
        return CreateResponseSchema(
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
                    ["summary"] = CreateNullablePrimitiveSchema("string"),
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
        var itemSchema = CreateValueSchema(typeof(Contracts.CodeActions.CodeActionListItem));
        var truncationSchema = CreateValueSchema(typeof(IReadOnlyList<Contracts.Results.CollectionTruncation>));

        return CreateResponseSchema(
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
                    ["items"] = CreateArraySchema(itemSchema),
                    ["hasMore"] = new JsonObject
                    {
                        ["type"] = "boolean",
                    },
                    ["truncatedBy"] = AllowNull(truncationSchema),
                },
            },
            [itemSchema, truncationSchema]);
    }

    private static JsonElement CreateResponseSchema(JsonObject successSchema, IReadOnlyList<JsonElement> componentSchemas)
    {
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
