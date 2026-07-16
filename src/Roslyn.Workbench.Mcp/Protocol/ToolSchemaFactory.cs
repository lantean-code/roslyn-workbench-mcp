using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class ToolSchemaFactory
{
    private readonly ConcurrentDictionary<Type, JsonElement> _directOutputSchemaCache = [];
    private readonly IMcpSdkSchemaProvider _schemaProvider;

    public ToolSchemaFactory(IMcpSdkSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public JsonElement CreateInputSchema<TRequest>()
    {
        return _schemaProvider.GetInputSchema<TRequest>();
    }

    public JsonElement CreateDirectOutputSchema(Type responseType)
    {
        return _directOutputSchemaCache.GetOrAdd(
            responseType,
            type => ToolSchemaBuilder.CreateDirectOutputSchema(
                _schemaProvider.GetValueSchema(type),
                _schemaProvider.GetValueSchema<Protocol.Results.ToolError>(),
                _schemaProvider.GetValueSchema<Workspace.Contracts.Results.RequiredAction>()));
    }

    public JsonElement CreateOutputSchema(PublishedToolKind kind, Type responseType)
    {
        return kind == PublishedToolKind.Mutation
            ? CreateMutationResponseSchema()
            : CreateQueryResponseSchema(responseType);
    }

    private JsonElement CreateQueryResponseSchema(Type responseType)
    {
        var valueSchema = _schemaProvider.GetValueSchema(responseType);

        return CreateResponseSchema(
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

    private JsonElement CreateMutationResponseSchema()
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

    private JsonElement CreateResponseSchema(JsonObject successSchema, IReadOnlyList<JsonElement> componentSchemas)
    {
        return ToolSchemaBuilder.CreateResponseSchema(
            successSchema,
            componentSchemas,
            _schemaProvider.GetValueSchema<Protocol.Results.ToolError>(),
            _schemaProvider.GetValueSchema<Workspace.Contracts.Results.RequiredAction>());
    }
}
