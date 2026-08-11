using System.Reflection;
using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal sealed class PluginHandlerWarningInspector : IPluginHandlerWarningInspector
{
    public IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var hasInstanceFields = false;
        var hasMutableMembers = false;
        var hasMutableStaticFields = false;
        var hasDisposableFields = false;
        var hasLegacyMetadata = false;
        foreach (var type in GetTypeHierarchy(handlerType))
        {
            var instanceFields = type.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (instanceFields.Any(static field => !IsIgnoredInstanceField(field)))
            {
                hasInstanceFields = true;
            }

            if (ContainsDisposableField(instanceFields, ignoreNonOwnedInstanceFields: true))
            {
                hasDisposableFields = true;
            }

            var properties = type.GetProperties(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (ContainsWritableProperty(properties))
            {
                hasMutableMembers = true;
            }

            var events = type.GetEvents(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (events.Length > 0)
            {
                hasMutableMembers = true;
            }

            var staticFields = type.GetFields(
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (ContainsMutableStaticField(staticFields))
            {
                hasMutableStaticFields = true;
            }

            if (ContainsDisposableField(staticFields, ignoreNonOwnedInstanceFields: false))
            {
                hasDisposableFields = true;
            }

            if (ContainsLegacyMetadata(staticFields))
            {
                hasLegacyMetadata = true;
            }
        }

        var handlerName = handlerType.FullName;
        if (hasInstanceFields)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerInstanceState,
                $"Plugin handler '{handlerName}' declares instance state and must remain thread-safe.");

            diagnostics.Add(diagnostic);
        }

        if (hasMutableMembers)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerMutableMembers,
                $"Plugin handler '{handlerName}' exposes mutable properties or events.");

            diagnostics.Add(diagnostic);
        }

        if (hasMutableStaticFields)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerStaticState,
                $"Plugin handler '{handlerName}' declares mutable static state.");

            diagnostics.Add(diagnostic);
        }

        if (hasDisposableFields)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerDisposableField,
                $"Plugin handler '{handlerName}' declares a field that may own a disposable resource.");

            diagnostics.Add(diagnostic);
        }

        if (hasLegacyMetadata)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.LegacyRegistration,
                $"Plugin handler '{handlerName}' declares legacy static registration metadata.");

            diagnostics.Add(diagnostic);
        }

        return diagnostics;
    }

    private static bool ContainsDisposableField(
        FieldInfo[] fields,
        bool ignoreNonOwnedInstanceFields)
    {
        foreach (var field in fields)
        {
            if (ignoreNonOwnedInstanceFields && IsIgnoredInstanceField(field))
            {
                continue;
            }

            var fieldType = field.FieldType;
            if (typeof(IDisposable).IsAssignableFrom(fieldType))
            {
                return true;
            }

            if (typeof(IAsyncDisposable).IsAssignableFrom(fieldType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIgnoredInstanceField(FieldInfo field)
    {
        if (!field.IsInitOnly)
        {
            return false;
        }

        var fieldName = GetConstructorParameterName(field.Name);
        var declaringType = field.DeclaringType;
        if (declaringType is null)
        {
            return false;
        }

        var constructors = declaringType.GetConstructors(
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly);

        return constructors.Length > 0
            && constructors.All(constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == field.FieldType
                && string.Equals(parameter.Name, fieldName, StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetConstructorParameterName(string fieldName)
    {
        if (fieldName.StartsWith('<'))
        {
            var closingDelimiterIndex = fieldName.IndexOf('>', StringComparison.Ordinal);
            if (closingDelimiterIndex > 1)
            {
                return fieldName[1..closingDelimiterIndex];
            }
        }

        return fieldName.TrimStart('_');
    }

    private static bool ContainsWritableProperty(PropertyInfo[] properties)
    {
        foreach (var property in properties)
        {
            if (property.SetMethod is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsMutableStaticField(FieldInfo[] fields)
    {
        foreach (var field in fields)
        {
            if (!field.IsLiteral && !field.IsInitOnly)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLegacyMetadata(FieldInfo[] fields)
    {
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(ToolRegistrationMetadata))
            {
                return true;
            }
        }

        return false;
    }

    private static DiagnosticInfo CreateDiagnostic(string id, string message)
    {
        var diagnostic = new DiagnosticInfo
        {
            Id = id,
            Severity = ContractDiagnosticSeverity.Warning,
            Message = message,
        };

        return diagnostic;
    }

    private static IEnumerable<Type> GetTypeHierarchy(Type handlerType)
    {
        for (var type = handlerType; type is not null; type = type.BaseType)
        {
            yield return type;
        }
    }
}
