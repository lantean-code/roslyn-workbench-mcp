using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Generates and caches contract schemas through the MCP SDK's tool-schema pipeline.
/// </summary>
internal sealed class McpSdkSchemaProvider : IMcpSdkSchemaProvider
{
    private static readonly AIJsonSchemaCreateOptions _inputSchemaCreateOptions = new()
    {
        TransformSchemaNode = InputContractSchemaTransformer.Transform,
    };

    private static readonly AIJsonSchemaCreateOptions _valueSchemaCreateOptions = new()
    {
        TransformSchemaNode = ContractDescriptionSchemaTransformer.Transform,
    };

    private readonly ConcurrentDictionary<Type, JsonElement> _inputSchemaCache = [];
    private readonly ConcurrentDictionary<Type, JsonElement> _valueSchemaCache = [];

    /// <summary>
    /// Gets the cached input schema for a request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <returns>The input schema.</returns>
    public JsonElement GetInputSchema<TRequest>()
    {
        return _inputSchemaCache.GetOrAdd(typeof(TRequest), CreateInputSchemaCore);
    }

    /// <summary>
    /// Gets the cached input schema for a request type selected at runtime.
    /// </summary>
    /// <param name="requestType">The request type to describe.</param>
    /// <returns>The schema rooted at the request object.</returns>
    public JsonElement GetInputSchemaForType(Type requestType)
    {
        return _inputSchemaCache.GetOrAdd(requestType, CreateInputSchemaCore);
    }

    /// <summary>
    /// Gets the cached JSON schema for a value type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>The value schema.</returns>
    public JsonElement GetValueSchema<TValue>()
    {
        return GetValueSchema(typeof(TValue));
    }

    /// <summary>
    /// Gets the cached JSON schema for a value type selected at runtime.
    /// </summary>
    /// <param name="valueType">The value type to describe.</param>
    /// <returns>The normalized schema for values of the specified type.</returns>
    public JsonElement GetValueSchema(Type valueType)
    {
        return _valueSchemaCache.GetOrAdd(valueType, CreateValueSchemaCore);
    }

    private static JsonElement CreateInputSchemaCore(Type requestType)
    {
        var probeType = typeof(SchemaProbe<>).MakeGenericType(requestType);
        var method = GetRequiredMethod(probeType, nameof(SchemaProbe<>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(
            method,
            target: null,
            new McpServerToolCreateOptions
            {
                SchemaCreateOptions = _inputSchemaCreateOptions,
                SerializerOptions = McpJsonOptions.Schema,
            });

        var root = tool.ProtocolTool.InputSchema;
        return InputSchemaExporter.ExtractRequestSchema(root);
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
                SerializerOptions = McpJsonOptions.Schema,
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
