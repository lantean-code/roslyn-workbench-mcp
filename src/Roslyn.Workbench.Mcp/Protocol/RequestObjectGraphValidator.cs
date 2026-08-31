using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Recursively validates deserialized request objects using data annotations.
/// </summary>
internal sealed class RequestObjectGraphValidator : IRequestObjectGraphValidator
{
    /// <summary>
    /// Validates a request and combines all discovered failures into one client-facing message.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="serializerOptions">The serializer settings used to format property names and validation values.</param>
    /// <param name="errorMessage">When validation fails, a message describing the invalid request fields.</param>
    /// <returns><see langword="true"/> when the request is invalid; otherwise, <see langword="false"/>.</returns>
    public bool TryCreateInvalidRequestError(
        object request,
        JsonSerializerOptions serializerOptions,
        [NotNullWhen(true)] out string? errorMessage)
    {
        var invalidPaths = new HashSet<string>(StringComparer.Ordinal);
        var validationFailures = new List<RequestValidationFailure>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateNode(request, string.Empty, serializerOptions, visited, invalidPaths, validationFailures);

        if (invalidPaths.Count == 0 && validationFailures.Count == 0)
        {
            errorMessage = null;
            return false;
        }

        if (validationFailures.Count > 0)
        {
            errorMessage = CreateValidationFailureError(invalidPaths, validationFailures);
            return true;
        }

        errorMessage = CreateInvalidPathsError(invalidPaths);
        return true;
    }

    private static string CreateInvalidPathsError(HashSet<string> invalidPaths)
    {
        var orderedPaths = invalidPaths.Order(StringComparer.Ordinal);
        var valueLabel = invalidPaths.Count == 1
            ? "value"
            : "values";

        var argumentLabel = invalidPaths.Count == 1
            ? "argument"
            : "arguments";

        var argumentNames = string.Join("', '", orderedPaths);
        return $"Invalid {valueLabel} for tool {argumentLabel}: '{argumentNames}'.";
    }

    private static string CreateValidationFailureError(
        HashSet<string> invalidPaths,
        List<RequestValidationFailure> validationFailures)
    {
        var details = new List<string>();
        foreach (var invalidPath in invalidPaths.Order(StringComparer.Ordinal))
        {
            details.Add($"'{invalidPath}' is invalid");
        }

        foreach (var failure in validationFailures
            .OrderBy(static item => item.Paths[0], StringComparer.Ordinal)
            .ThenBy(static item => item.Message, StringComparer.Ordinal))
        {
            var paths = string.Join("', '", failure.Paths);
            details.Add($"'{paths}': {failure.Message}");
        }

        var joinedDetails = string.Join("; ", details).TrimEnd('.');
        return $"Invalid tool arguments: {joinedDetails}.";
    }

    private static void ValidateNode(
        object? value,
        string path,
        JsonSerializerOptions serializerOptions,
        HashSet<object> visited,
        HashSet<string> invalidPaths,
        List<RequestValidationFailure> validationFailures)
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
                ValidateNode(
                    item,
                    $"{path}[{index}]",
                    serializerOptions,
                    visited,
                    invalidPaths,
                    validationFailures);
                index++;
            }

            return;
        }

        ValidateObject(value, path, serializerOptions, validationFailures);

        var typeInfo = serializerOptions.GetTypeInfo(type);
        foreach (var property in typeInfo.Properties)
        {
            if (property.Get is null)
            {
                continue;
            }

            var propertyValue = property.Get(value);
            var propertyPath = string.IsNullOrEmpty(path)
                ? property.Name
                : $"{path}.{property.Name}";

            ValidateNode(
                propertyValue,
                propertyPath,
                serializerOptions,
                visited,
                invalidPaths,
                validationFailures);
        }
    }

    private static void ValidateObject(
        object value,
        string path,
        JsonSerializerOptions serializerOptions,
        List<RequestValidationFailure> validationFailures)
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
                var objectPath = string.IsNullOrEmpty(path) ? "request" : path;
                var message = validationResult.ErrorMessage ?? "The value is invalid.";
                validationFailures.Add(new RequestValidationFailure([objectPath], message));
                continue;
            }

            var memberPaths = new string[memberNames.Length];
            var memberIndex = 0;
            foreach (var memberName in memberNames)
            {
                var jsonMemberName = GetJsonMemberName(value.GetType(), memberName, serializerOptions);
                var memberPath = string.IsNullOrEmpty(path)
                    ? jsonMemberName
                    : $"{path}.{jsonMemberName}";

                memberPaths[memberIndex] = memberPath;
                memberIndex++;
            }

            Array.Sort(memberPaths, StringComparer.Ordinal);
            var failureMessage = validationResult.ErrorMessage ?? "The value is invalid.";
            validationFailures.Add(new RequestValidationFailure(memberPaths, failureMessage));
        }
    }

    private static string GetJsonMemberName(
        Type declaringType,
        string memberName,
        JsonSerializerOptions serializerOptions)
    {
        var typeInfo = serializerOptions.GetTypeInfo(declaringType);
        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is System.Reflection.MemberInfo member
                && string.Equals(member.Name, memberName, StringComparison.Ordinal))
            {
                return property.Name;
            }
        }

        return serializerOptions.PropertyNamingPolicy?.ConvertName(memberName) ?? memberName;
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

    private sealed record RequestValidationFailure
    {
        public IReadOnlyList<string> Paths { get; }

        public string Message { get; }

        public RequestValidationFailure(IReadOnlyList<string> paths, string message)
        {
            Paths = paths;
            Message = message;
        }
    }
}
