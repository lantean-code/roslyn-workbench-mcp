using System.Buffers;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolRequestBinder
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public static TRequest Deserialize<TRequest>(IDictionary<string, JsonElement> arguments)
        where TRequest : class
    {

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

        var request = JsonSerializer.Deserialize<TRequest>(buffer.WrittenSpan, _serializerOptions);

        if (request is null)
        {
            throw new JsonException($"Request payload for '{typeof(TRequest).FullName}' could not be deserialized.");
        }

        return request;
    }
}
