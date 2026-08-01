using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.Protocol;

internal sealed class RequestObjectGraphValidator : IRequestObjectGraphValidator
{
    private readonly IWorkspaceContractValidator<DocumentSelector> _documentSelectorValidator;
    private readonly IWorkspaceContractValidator<LocationSelector> _locationSelectorValidator;
    private readonly IWorkspaceContractValidator<ProjectSelector> _projectSelectorValidator;
    private readonly IWorkspaceContractValidator<ScopeSelector> _scopeSelectorValidator;
    private readonly IWorkspaceContractValidator<SymbolSelector> _symbolSelectorValidator;
    private readonly IWorkspaceContractValidator<WorkspaceSelector> _workspaceSelectorValidator;

    public RequestObjectGraphValidator(
        IWorkspaceContractValidator<DocumentSelector> documentSelectorValidator,
        IWorkspaceContractValidator<LocationSelector> locationSelectorValidator,
        IWorkspaceContractValidator<ProjectSelector> projectSelectorValidator,
        IWorkspaceContractValidator<ScopeSelector> scopeSelectorValidator,
        IWorkspaceContractValidator<SymbolSelector> symbolSelectorValidator,
        IWorkspaceContractValidator<WorkspaceSelector> workspaceSelectorValidator)
    {
        _documentSelectorValidator = documentSelectorValidator;
        _locationSelectorValidator = locationSelectorValidator;
        _projectSelectorValidator = projectSelectorValidator;
        _scopeSelectorValidator = scopeSelectorValidator;
        _symbolSelectorValidator = symbolSelectorValidator;
        _workspaceSelectorValidator = workspaceSelectorValidator;
    }

    public bool TryCreateInvalidRequestError(
        object request,
        JsonSerializerOptions serializerOptions,
        [NotNullWhen(true)] out string? errorMessage)
    {
        var invalidPaths = new HashSet<string>(StringComparer.Ordinal);
        var selectorFailures = new List<SelectorFailure>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateNode(request, string.Empty, serializerOptions, visited, invalidPaths, selectorFailures);

        if (invalidPaths.Count == 0 && selectorFailures.Count == 0)
        {
            errorMessage = null;
            return false;
        }

        if (selectorFailures.Count > 0)
        {
            errorMessage = CreateSelectorFailureError(invalidPaths, selectorFailures);
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

    private static string CreateSelectorFailureError(
        HashSet<string> invalidPaths,
        List<SelectorFailure> selectorFailures)
    {
        var details = new List<string>();
        foreach (var invalidPath in invalidPaths.Order(StringComparer.Ordinal))
        {
            details.Add($"'{invalidPath}' is invalid");
        }

        foreach (var failure in selectorFailures
            .OrderBy(static item => item.Paths[0], StringComparer.Ordinal)
            .ThenBy(static item => item.Message, StringComparer.Ordinal))
        {
            var paths = string.Join("', '", failure.Paths);
            details.Add($"'{paths}': {failure.Message}");
        }

        return $"Invalid tool arguments: {string.Join("; ", details)}.";
    }

    private void ValidateNode(
        object? value,
        string path,
        JsonSerializerOptions serializerOptions,
        HashSet<object> visited,
        HashSet<string> invalidPaths,
        List<SelectorFailure> selectorFailures)
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
                    selectorFailures);
                index++;
            }

            return;
        }

        ValidateObject(value, path, serializerOptions, invalidPaths);
        ValidateSelector(value, path, serializerOptions, selectorFailures);

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
                selectorFailures);
        }
    }

    private void ValidateSelector(
        object value,
        string path,
        JsonSerializerOptions serializerOptions,
        List<SelectorFailure> selectorFailures)
    {
        switch (value)
        {
            case DocumentSelector selector:
                AddSelectorFailures(_documentSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;

            case LocationSelector selector:
                AddSelectorFailures(_locationSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;

            case ProjectSelector selector:
                AddSelectorFailures(_projectSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;

            case ScopeSelector selector:
                AddSelectorFailures(_scopeSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;

            case SymbolSelector selector:
                AddSelectorFailures(_symbolSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;

            case WorkspaceSelector selector:
                AddSelectorFailures(_workspaceSelectorValidator, selector, path, serializerOptions, selectorFailures);
                break;
        }
    }

    private static void AddSelectorFailures<TSelector>(
        IWorkspaceContractValidator<TSelector> validator,
        TSelector selector,
        string path,
        JsonSerializerOptions serializerOptions,
        List<SelectorFailure> selectorFailures)
        where TSelector : class
    {
        var validationResult = validator.Validate(selector);
        if (validationResult.IsValid)
        {
            return;
        }

        foreach (var failure in validationResult.Failures)
        {
            var paths = failure.MemberNames
                .Select(memberName => GetJsonMemberName(typeof(TSelector), memberName, serializerOptions))
                .Select(memberName => string.IsNullOrEmpty(path) ? memberName : $"{path}.{memberName}")
                .Order(StringComparer.Ordinal)
                .ToArray();

            selectorFailures.Add(new SelectorFailure(paths, failure.Message));
        }
    }

    private static void ValidateObject(
        object value,
        string path,
        JsonSerializerOptions serializerOptions,
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
                invalidPaths.Add(string.IsNullOrEmpty(path) ? "request" : path);
                continue;
            }

            foreach (var memberName in memberNames)
            {
                var jsonMemberName = GetJsonMemberName(value.GetType(), memberName, serializerOptions);
                var memberPath = string.IsNullOrEmpty(path)
                    ? jsonMemberName
                    : $"{path}.{jsonMemberName}";

                invalidPaths.Add(memberPath);
            }
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

    private sealed record SelectorFailure
    {
        public IReadOnlyList<string> Paths { get; }

        public string Message { get; }

        public SelectorFailure(IReadOnlyList<string> paths, string message)
        {
            Paths = paths;
            Message = message;
        }
    }
}
