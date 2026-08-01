using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class McpSdkSchemaProvider : IMcpSdkSchemaProvider
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    private static readonly AIJsonSchemaCreateOptions _inputSchemaCreateOptions = new()
    {
        TransformSchemaNode = InputContractSchemaTransformer.Transform,
    };

    private static readonly AIJsonSchemaCreateOptions _valueSchemaCreateOptions = new();

    private readonly ConcurrentDictionary<Type, JsonElement> _inputSchemaCache = [];
    private readonly ConcurrentDictionary<Type, JsonElement> _valueSchemaCache = [];

    public JsonElement GetInputSchema<TRequest>()
    {
        return _inputSchemaCache.GetOrAdd(typeof(TRequest), static _ => CreateInputSchemaCore<TRequest>());
    }

    public JsonElement GetValueSchema<TValue>()
    {
        return GetValueSchema(typeof(TValue));
    }

    public JsonElement GetValueSchema(Type valueType)
    {
        return _valueSchemaCache.GetOrAdd(valueType, CreateValueSchemaCore);
    }

    private static JsonElement CreateInputSchemaCore<TRequest>()
    {
        var method = GetRequiredMethod(typeof(SchemaProbe<TRequest>), nameof(SchemaProbe<>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(
            method,
            target: null,
            new McpServerToolCreateOptions
            {
                SchemaCreateOptions = _inputSchemaCreateOptions,
                SerializerOptions = _serializerOptions,
            });

        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");

        return ToolSchemaBuilder.NormalizeExportedSchema(requestSchema, root);
    }

    private JsonElement CreateValueSchemaCore(Type valueType)
    {
        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(BoundedCollection<>))
        {
            var itemSchema = GetValueSchema(valueType.GetGenericArguments()[0]);
            return ToolSchemaBuilder.CreateBoundedCollectionSchema(itemSchema);
        }

        var method = GetRequiredMethod(typeof(McpSdkSchemaProvider), nameof(CreateValueSchemaCoreGeneric), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(valueType);

        var invocationResult = method.Invoke(null, null);
        if (invocationResult is not JsonElement schema)
        {
            throw new InvalidOperationException("Schema generation did not return a JSON element.");
        }

        return schema;
    }

    private static JsonElement CreateValueSchemaCoreGeneric<TValue>()
    {
        var method = GetRequiredMethod(typeof(SchemaValueProbe<TValue>), nameof(SchemaValueProbe<>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(
            method,
            target: null,
            new McpServerToolCreateOptions
            {
                SchemaCreateOptions = _valueSchemaCreateOptions,
                SerializerOptions = _serializerOptions,
            });

        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");
        var valueSchema = requestSchema.GetProperty("properties").GetProperty("value");

        return ToolSchemaBuilder.NormalizeExportedSchema(valueSchema, root);
    }

    private static MethodInfo GetRequiredMethod(Type type, string name, BindingFlags bindingFlags)
    {
        var method = type.GetMethod(name, bindingFlags);
        if (method is null)
        {
            throw new InvalidOperationException($"Schema method '{type.FullName}.{name}' was not found.");
        }

        return method;
    }

    private sealed record SchemaValueWrapper<TValue>
    {
        public TValue? Value { get; init; }
    }

    private static class SchemaProbe<TRequest>
    {
        [McpServerTool(Name = "schema-input-probe")]
        public static string Invoke(TRequest request)
        {
            return string.Empty;
        }
    }

    private static class SchemaValueProbe<TValue>
    {
        [McpServerTool(Name = "schema-value-probe")]
        public static string Invoke(SchemaValueWrapper<TValue> request)
        {
            return string.Empty;
        }
    }
}
