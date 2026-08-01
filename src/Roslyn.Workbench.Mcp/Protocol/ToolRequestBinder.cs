using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            var metadata = RequestMetadata<TRequest>.Value;
            var foundRequiredArguments = metadata.RequiredNames.Length == 0
                ? null
                : new bool[metadata.RequiredNames.Length];

            var foundEnumArguments = metadata.EnumArguments.Length == 0
                ? null
                : new bool[metadata.EnumArguments.Length];

            var buffer = SerializeArguments(
                arguments,
                metadata.RequiredIndexes,
                foundRequiredArguments,
                metadata.EnumIndexes,
                foundEnumArguments);

            if (foundRequiredArguments is not null
                && TryCreateMissingArgumentsError(metadata.RequiredNames, foundRequiredArguments, out errorMessage))
            {
                request = null;
                return false;
            }

            request = Deserialize<TRequest>(buffer);
            if (foundEnumArguments is not null
                && TryCreateUndefinedEnumArgumentsError(request, metadata.EnumArguments, foundEnumArguments, out errorMessage))
            {
                request = null;
                return false;
            }

            if (TryCreateInvalidArgumentsError(request, metadata.ValidationArguments, out errorMessage))
            {
                request = null;
                return false;
            }

            if (RequestObjectGraphValidator.TryCreateInvalidDescendantsError(
                request,
                _serializerOptions,
                out errorMessage))
            {
                request = null;
                return false;
            }

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
        bool[]? foundRequiredArguments,
        Dictionary<string, int>? enumArgumentIndexes,
        bool[]? foundEnumArguments)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var pair in arguments)
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);

                if (requiredArgumentIndexes is not null
                    && foundRequiredArguments is not null
                    && requiredArgumentIndexes.TryGetValue(pair.Key, out var requiredArgumentIndex))
                {
                    foundRequiredArguments[requiredArgumentIndex] = true;
                }

                if (enumArgumentIndexes is not null
                    && foundEnumArguments is not null
                    && enumArgumentIndexes.TryGetValue(pair.Key, out var enumArgumentIndex))
                {
                    foundEnumArguments[enumArgumentIndex] = true;
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

    private static bool TryCreateUndefinedEnumArgumentsError<TRequest>(
        TRequest request,
        EnumArgumentMetadata[] enumArguments,
        bool[] foundEnumArguments,
        [NotNullWhen(true)] out string? errorMessage)
        where TRequest : class
    {
        List<string>? undefinedArguments = null;

        for (var index = 0; index < enumArguments.Length; index++)
        {
            if (!foundEnumArguments[index])
            {
                continue;
            }

            var enumArgument = enumArguments[index];
            var value = enumArgument.Getter(request);
            if (value is null || IsDefinedEnumValue(enumArgument.Type, value))
            {
                continue;
            }

            undefinedArguments ??= [];
            undefinedArguments.Add(enumArgument.Name);
        }

        if (undefinedArguments is null)
        {
            errorMessage = null;
            return false;
        }

        var valueLabel = undefinedArguments.Count == 1
            ? "value"
            : "values";

        var argumentLabel = undefinedArguments.Count == 1
            ? "argument"
            : "arguments";

        var argumentNames = string.Join("', '", undefinedArguments);
        errorMessage = $"Unsupported {valueLabel} for tool {argumentLabel}: '{argumentNames}'.";
        return true;
    }

    private static bool IsDefinedEnumValue(Type enumType, object value)
    {
        if (Enum.IsDefined(enumType, value))
        {
            return true;
        }

        if (!enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            return false;
        }

        var definedBits = 0UL;
        foreach (var definedValue in Enum.GetValues(enumType))
        {
            definedBits |= ConvertEnumToUInt64(enumType, definedValue);
        }

        var valueBits = ConvertEnumToUInt64(enumType, value);
        return (valueBits & ~definedBits) == 0;
    }

    private static ulong ConvertEnumToUInt64(Type enumType, object value)
    {
        var typeCode = Type.GetTypeCode(Enum.GetUnderlyingType(enumType));
        if (typeCode is TypeCode.SByte
            or TypeCode.Int16
            or TypeCode.Int32
            or TypeCode.Int64)
        {
            return unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    }

    private static bool TryCreateInvalidArgumentsError<TRequest>(
        TRequest request,
        ValidationArgumentMetadata[] validationArguments,
        [NotNullWhen(true)] out string? errorMessage)
        where TRequest : class
    {
        List<string>? invalidArguments = null;

        foreach (var argument in validationArguments)
        {
            var value = argument.Getter(request);
            var validationContext = new ValidationContext(request)
            {
                DisplayName = argument.Name,
                MemberName = argument.Name,
            };

            if (argument.Attributes.All(attribute =>
                attribute.GetValidationResult(value, validationContext) == ValidationResult.Success))
            {
                continue;
            }

            invalidArguments ??= [];
            invalidArguments.Add(argument.Name);
        }

        if (invalidArguments is null)
        {
            errorMessage = null;
            return false;
        }

        var valueLabel = invalidArguments.Count == 1
            ? "value"
            : "values";

        var argumentLabel = invalidArguments.Count == 1
            ? "argument"
            : "arguments";

        var argumentNames = string.Join("', '", invalidArguments);
        errorMessage = $"Invalid {valueLabel} for tool {argumentLabel}: '{argumentNames}'.";
        return true;
    }

    private static ToolRequestBindingMetadata CreateRequestMetadata(Type requestType)
    {
        var typeInfo = _serializerOptions.GetTypeInfo(requestType);
        var requiredArgumentNames = new List<string>();
        var enumArguments = new List<EnumArgumentMetadata>();
        var validationArguments = new List<ValidationArgumentMetadata>();

        foreach (var property in typeInfo.Properties)
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (propertyType.IsEnum && property.Get is not null)
            {
                enumArguments.Add(new EnumArgumentMetadata(property.Name, propertyType, property.Get));
            }

            var validationAttributes = property.AttributeProvider?
                .GetCustomAttributes(typeof(ValidationAttribute), inherit: true)
                .OfType<ValidationAttribute>()
                .ToArray();

            if (property.IsRequired
                || validationAttributes?.Any(static attribute => attribute is RequiredAttribute) == true)
            {
                requiredArgumentNames.Add(property.Name);
            }

            if (validationAttributes is { Length: > 0 } && property.Get is not null)
            {
                validationArguments.Add(new ValidationArgumentMetadata(
                    property.Name,
                    property.Get,
                    validationAttributes));
            }
        }

        requiredArgumentNames.Sort(StringComparer.Ordinal);
        enumArguments.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        validationArguments.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

        var requiredNames = requiredArgumentNames.ToArray();
        var requiredIndexes = new Dictionary<string, int>(requiredNames.Length, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < requiredNames.Length; index++)
        {
            requiredIndexes.Add(requiredNames[index], index);
        }

        var enumArgumentArray = enumArguments.ToArray();
        var enumIndexes = new Dictionary<string, int>(enumArgumentArray.Length, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < enumArgumentArray.Length; index++)
        {
            enumIndexes.Add(enumArgumentArray[index].Name, index);
        }

        return new ToolRequestBindingMetadata(
            requiredNames,
            requiredIndexes,
            enumArgumentArray,
            enumIndexes,
            validationArguments.ToArray());
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
        public static ToolRequestBindingMetadata Value { get; } = CreateRequestMetadata(typeof(TRequest));
    }
}
