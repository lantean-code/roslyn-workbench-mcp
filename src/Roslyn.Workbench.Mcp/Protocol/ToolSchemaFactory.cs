using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class ToolSchemaFactory
{
    private readonly ConcurrentDictionary<Type, JsonElement> _directOutputSchemaCache = [];
    private readonly ConcurrentDictionary<Type, JsonElement> _inputSchemaCache = [];
    private readonly IMcpSdkSchemaProvider _schemaProvider;

    public ToolSchemaFactory(IMcpSdkSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public JsonElement CreateInputSchema<TRequest>()
    {
        return _inputSchemaCache.GetOrAdd(
            typeof(TRequest),
            static (_, schemaProvider) => InputSchemaDefaultPublisher.Publish(
                schemaProvider.GetInputSchema<TRequest>(),
                typeof(TRequest)),
            _schemaProvider);
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
        if (kind == PublishedToolKind.Mutation)
        {
            return CreateMutationResponseSchema();
        }

        return CreateQueryResponseSchema(responseType);
    }

    private JsonElement CreateQueryResponseSchema(Type responseType)
    {
        var valueSchema = _schemaProvider.GetValueSchema(responseType);
        var dataSchema = JsonNode.Parse(valueSchema.GetRawText());
        var successSchema = ToolSchemaBuilder.CreateSuccessSchema(dataSchema);

        return CreateResponseSchema(successSchema, [valueSchema]);
    }

    private JsonElement CreateMutationResponseSchema()
    {
        var stagedSchema = new JsonObject
        {
            ["type"] = "boolean",
        };

        var revisionSchema = new JsonObject
        {
            ["type"] = "integer",
        };

        var transactionProperties = new JsonObject
        {
            ["revision"] = revisionSchema,
        };

        var transactionTypes = new JsonArray("object", "null");
        var requiredTransactionProperties = new JsonArray("revision");
        var transactionSchema = new JsonObject
        {
            ["type"] = transactionTypes,
            ["required"] = requiredTransactionProperties,
            ["properties"] = transactionProperties,
        };

        var mutationProperties = new JsonObject
        {
            ["staged"] = stagedSchema,
            ["summary"] = ToolSchemaBuilder.CreateNullablePrimitiveSchema("string"),
            ["transaction"] = transactionSchema,
        };

        var requiredMutationProperties = new JsonArray("staged");
        var mutationDataSchema = new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredMutationProperties,
            ["properties"] = mutationProperties,
        };

        var successSchema = ToolSchemaBuilder.CreateSuccessSchema(mutationDataSchema);
        return CreateResponseSchema(successSchema, []);
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
