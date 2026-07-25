using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class ToolRequestBinder
{
    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    public static bool TryBind<TRequest>(
        IDictionary<string, JsonElement> arguments,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out string? errorMessage)
        where TRequest : class
    {
        try
        {
            var (requiredArgumentNames, requiredArgumentIndexes) = RequestMetadata<TRequest>.RequiredArguments;
            var foundRequiredArguments = requiredArgumentNames.Length == 0
                ? null
                : new bool[requiredArgumentNames.Length];

            var buffer = SerializeArguments(arguments, requiredArgumentIndexes, foundRequiredArguments);

            if (foundRequiredArguments is not null
                && TryCreateMissingArgumentsError(requiredArgumentNames, foundRequiredArguments, out errorMessage))
            {
                request = null;
                return false;
            }

            request = Deserialize<TRequest>(buffer);
            errorMessage = null;
            return true;
        }
        catch (JsonException exception)
        {
            request = null;
            errorMessage = $"The tool arguments did not match the request contract. {exception.Message}";
            return false;
        }
    }

    private static TRequest Deserialize<TRequest>(ArrayBufferWriter<byte> buffer)
        where TRequest : class
    {
        var request = JsonSerializer.Deserialize<TRequest>(buffer.WrittenSpan, _serializerOptions);

        if (request is null)
        {
            throw new JsonException($"Request payload for '{typeof(TRequest).FullName}' could not be deserialized.");
        }

        return request;
    }

    private static ArrayBufferWriter<byte> SerializeArguments(
        IDictionary<string, JsonElement> arguments,
        Dictionary<string, int>? requiredArgumentIndexes,
        bool[]? foundRequiredArguments)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var pair in arguments)
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);

                if (requiredArgumentIndexes is null || foundRequiredArguments is null)
                {
                    continue;
                }

                if (requiredArgumentIndexes.TryGetValue(pair.Key, out var requiredArgumentIndex))
                {
                    foundRequiredArguments[requiredArgumentIndex] = true;
                }
            }

            writer.WriteEndObject();
        }

        return buffer;
    }

    private static bool TryCreateMissingArgumentsError(
        string[] requiredArgumentNames,
        bool[] foundRequiredArguments,
        [NotNullWhen(true)] out string? errorMessage)
    {
        List<string>? missingArguments = null;

        for (var index = 0; index < requiredArgumentNames.Length; index++)
        {
            if (foundRequiredArguments[index])
            {
                continue;
            }

            missingArguments ??= [];
            missingArguments.Add(requiredArgumentNames[index]);
        }

        if (missingArguments is null)
        {
            errorMessage = null;
            return false;
        }

        var argumentLabel = missingArguments.Count == 1
            ? "argument"
            : "arguments";

        var argumentNames = string.Join("', '", missingArguments);
        errorMessage = $"Missing required tool {argumentLabel}: '{argumentNames}'.";
        return true;
    }

    private static (string[] Names, Dictionary<string, int> Indexes) CreateRequiredArgumentMetadata(Type requestType)
    {
        var typeInfo = _serializerOptions.GetTypeInfo(requestType);
        var requiredArgumentNames = new List<string>();

        foreach (var property in typeInfo.Properties)
        {
            if (property.IsRequired)
            {
                requiredArgumentNames.Add(property.Name);
            }
        }

        requiredArgumentNames.Sort(StringComparer.Ordinal);
        var names = requiredArgumentNames.ToArray();
        var indexes = new Dictionary<string, int>(names.Length, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < names.Length; index++)
        {
            indexes.Add(names[index], index);
        }

        return (names, indexes);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectNullableAnnotations = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };

        serializerOptions.MakeReadOnly();
        return serializerOptions;
    }

    private static class RequestMetadata<TRequest>
        where TRequest : class
    {
        public static (string[] Names, Dictionary<string, int> Indexes) RequiredArguments { get; } =
            CreateRequiredArgumentMetadata(typeof(TRequest));
    }
}
