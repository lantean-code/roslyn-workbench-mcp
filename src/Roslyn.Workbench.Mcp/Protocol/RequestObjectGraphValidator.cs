using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal static class RequestObjectGraphValidator
{
    public static bool TryCreateInvalidDescendantsError(
        object request,
        JsonSerializerOptions serializerOptions,
        [NotNullWhen(true)] out string? errorMessage)
    {
        var invalidPaths = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var requestTypeInfo = serializerOptions.GetTypeInfo(request.GetType());

        foreach (var property in requestTypeInfo.Properties)
        {
            if (property.Get is null)
            {
                continue;
            }

            var value = property.Get(request);
            ValidateNode(value, property.Name, serializerOptions, visited, invalidPaths);
        }

        if (invalidPaths.Count == 0)
        {
            errorMessage = null;
            return false;
        }

        var orderedPaths = invalidPaths.Order(StringComparer.Ordinal);
        var valueLabel = invalidPaths.Count == 1
            ? "value"
            : "values";

        var argumentLabel = invalidPaths.Count == 1
            ? "argument"
            : "arguments";

        var argumentNames = string.Join("', '", orderedPaths);
        errorMessage = $"Invalid {valueLabel} for tool {argumentLabel}: '{argumentNames}'.";
        return true;
    }

    private static void ValidateNode(
        object? value,
        string path,
        JsonSerializerOptions serializerOptions,
        HashSet<object> visited,
        HashSet<string> invalidPaths)
    {
        if (value is null)
        {
            return;
        }

        var type = value.GetType();
        if (type.IsEnum)
        {
            if (!IsDefinedEnumValue(type, value))
            {
                invalidPaths.Add(path);
            }

            return;
        }

        if (IsTerminalType(type))
        {
            return;
        }

        if (!type.IsValueType && !visited.Add(value))
        {
            return;
        }

        if (value is IEnumerable values)
        {
            var index = 0;
            foreach (var item in values)
            {
                ValidateNode(item, $"{path}[{index}]", serializerOptions, visited, invalidPaths);
                index++;
            }

            return;
        }

        ValidateObject(value, path, invalidPaths);

        var typeInfo = serializerOptions.GetTypeInfo(type);
        foreach (var property in typeInfo.Properties)
        {
            if (property.Get is null)
            {
                continue;
            }

            var propertyValue = property.Get(value);
            var propertyPath = $"{path}.{property.Name}";
            ValidateNode(propertyValue, propertyPath, serializerOptions, visited, invalidPaths);
        }
    }

    private static void ValidateObject(
        object value,
        string path,
        HashSet<string> invalidPaths)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(value);
        Validator.TryValidateObject(
            value,
            validationContext,
            validationResults,
            validateAllProperties: true);

        foreach (var validationResult in validationResults)
        {
            var memberNames = validationResult.MemberNames.ToArray();
            if (memberNames.Length == 0)
            {
                invalidPaths.Add(path);
                continue;
            }

            foreach (var memberName in memberNames)
            {
                var jsonMemberName = JsonNamingPolicy.CamelCase.ConvertName(memberName);
                invalidPaths.Add($"{path}.{jsonMemberName}");
            }
        }
    }

    private static bool IsTerminalType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(Uri)
            || type == typeof(JsonElement)
            || type == typeof(JsonDocument);
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
}
