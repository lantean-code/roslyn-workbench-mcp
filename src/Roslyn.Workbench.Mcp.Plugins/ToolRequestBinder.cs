using System.Buffers;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins;

internal static class ToolRequestBinder
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public static TRequest Deserialize<TRequest>(IDictionary<string, JsonElement> arguments)
        where TRequest : class
    {
        return (TRequest)Deserialize(typeof(TRequest), arguments);
    }

    public static object Deserialize(Type requestType, IDictionary<string, JsonElement> arguments)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(arguments);

        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var pair in arguments)
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var request = JsonSerializer.Deserialize(buffer.WrittenSpan, requestType, _serializerOptions);

        if (request is null)
        {
            throw new JsonException($"Request payload for '{requestType.FullName}' could not be deserialized.");
        }

        return request;
    }
}
