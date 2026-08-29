using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class ToolSchemaFactory : IToolSchemaFactory
{
    private static readonly JsonElement _continuationSchema = ToolContinuationSchema.Create();

    private readonly ConcurrentDictionary<Type, JsonElement> _directOutputSchemaCache = [];
    private readonly ConcurrentDictionary<Type, JsonElement> _inputSchemaCache = [];
    private readonly ConcurrentDictionary<(PublishedToolKind Kind, Type ResponseType), JsonElement> _outputSchemaCache = [];
    private readonly IMcpSdkSchemaProvider _schemaProvider;

    public ToolSchemaFactory(IMcpSdkSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public JsonElement CreateInputSchema<TRequest>()
    {
        return _inputSchemaCache.GetOrAdd(
            typeof(TRequest),
            static (_, schemaProvider) => schemaProvider.GetInputSchema<TRequest>(),
            _schemaProvider);
    }

    public JsonElement CreateInputSchemaForType(Type requestType)
    {
        return _inputSchemaCache.GetOrAdd(
            requestType,
            static (type, schemaProvider) => schemaProvider.GetInputSchemaForType(type),
            _schemaProvider);
    }

    public JsonElement CreateDirectOutputSchema(Type responseType)
    {
        return _directOutputSchemaCache.GetOrAdd(
            responseType,
            type => ToolSchemaBuilder.CreateDirectOutputSchema(
                _schemaProvider.GetValueSchema(type),
                _schemaProvider.GetValueSchema<ToolError>(),
                _continuationSchema,
                _schemaProvider.GetValueSchema<SnapshotPrecondition>()));
    }

    public JsonElement CreateOutputSchema(PublishedToolKind kind, Type responseType)
    {
        return _outputSchemaCache.GetOrAdd(
            (kind, responseType),
            key => CreateOutputSchemaCore(key.Kind, key.ResponseType));
    }

    private JsonElement CreateOutputSchemaCore(PublishedToolKind kind, Type responseType)
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
        var snapshotSchema = _schemaProvider.GetValueSchema<SnapshotPrecondition>();
        var successSchema = ToolSchemaBuilder.CreateNullableSuccessSchema(
            valueSchema,
            snapshotSchema,
            snapshotRequired: true);

        return CreateResponseSchema(successSchema, [valueSchema, snapshotSchema]);
    }

    private JsonElement CreateMutationResponseSchema()
    {
        var stagedSchema = new JsonObject
        {
            ["type"] = "boolean",
            ["description"] = "Whether the mutation was staged in the active transaction.",
        };

        var revisionSchema = new JsonObject
        {
            ["type"] = "integer",
            ["description"] = "Transaction revision after staging.",
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
            ["description"] = "Transaction state after the mutation, when present.",
            ["required"] = requiredTransactionProperties,
            ["properties"] = transactionProperties,
        };

        var mutationProperties = new JsonObject
        {
            ["staged"] = stagedSchema,
            ["summary"] = CreateMutationSummarySchema(),
            ["transaction"] = transactionSchema,
        };

        var requiredMutationProperties = new JsonArray("staged");
        var mutationDataSchema = new JsonObject
        {
            ["type"] = "object",
            ["required"] = requiredMutationProperties,
            ["properties"] = mutationProperties,
        };

        var snapshotSchema = _schemaProvider.GetValueSchema<SnapshotPrecondition>();
        var successSchema = ToolSchemaBuilder.CreateSuccessSchema(
            mutationDataSchema,
            snapshotSchema,
            snapshotRequired: true);

        return CreateResponseSchema(successSchema, [snapshotSchema]);
    }

    private JsonElement CreateResponseSchema(JsonObject successSchema, IReadOnlyList<JsonElement> componentSchemas)
    {
        return ToolSchemaBuilder.CreateResponseSchema(
            successSchema,
            componentSchemas,
            _schemaProvider.GetValueSchema<ToolError>(),
            _continuationSchema);
    }

    private static JsonObject CreateMutationSummarySchema()
    {
        var schema = ToolSchemaBuilder.CreateNullablePrimitiveSchema("string");
        schema["description"] = "Summary of the staged mutation, when available.";
        return schema;
    }
}
