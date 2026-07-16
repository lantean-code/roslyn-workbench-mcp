using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class McpSdkSchemaProvider : IMcpSdkSchemaProvider
{
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
        var method = GetRequiredMethod(typeof(SchemaProbe<TRequest>), nameof(SchemaProbe<TRequest>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(method);
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

        return method.Invoke(null, null) is JsonElement schema
            ? schema
            : throw new InvalidOperationException("Schema generation did not return a JSON element.");
    }

    private static JsonElement CreateValueSchemaCoreGeneric<TValue>()
    {
        var method = GetRequiredMethod(typeof(SchemaValueProbe<TValue>), nameof(SchemaValueProbe<TValue>.Invoke), BindingFlags.Public | BindingFlags.Static);
        var tool = McpServerTool.Create(method);
        var root = tool.ProtocolTool.InputSchema;
        var requestSchema = root.GetProperty("properties").GetProperty("request");
        var valueSchema = requestSchema.GetProperty("properties").GetProperty("value");

        return ToolSchemaBuilder.NormalizeExportedSchema(valueSchema, root);
    }

    private static MethodInfo GetRequiredMethod(Type type, string name, BindingFlags bindingFlags)
    {
        return type.GetMethod(name, bindingFlags)
            ?? throw new InvalidOperationException($"Schema method '{type.FullName}.{name}' was not found.");
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
            _ = request;

            return string.Empty;
        }
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
