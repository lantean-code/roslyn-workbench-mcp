using System.Reflection;
using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Contracts.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal sealed class PluginHandlerWarningInspector : IPluginHandlerWarningInspector
{
    public IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType)
    {
        var hierarchy = GetTypeHierarchy(handlerType).ToArray();
        var diagnostics = new List<DiagnosticInfo>();
        if (hierarchy.SelectMany(static type => type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)).Any())
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.HandlerInstanceState,
                $"Plugin handler '{handlerType.FullName}' declares instance state and must remain thread-safe."));
        }

        var hasMutableMembers = hierarchy.Any(static type =>
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Any(static property => property.SetMethod is not null)
            || type.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length > 0);

        if (hasMutableMembers)
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.HandlerMutableMembers,
                $"Plugin handler '{handlerType.FullName}' exposes mutable properties or events."));
        }

        var staticFields = hierarchy.SelectMany(static type => type.GetFields(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)).ToArray();

        if (staticFields.Any(static field => !field.IsLiteral && !field.IsInitOnly))
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.HandlerStaticState,
                $"Plugin handler '{handlerType.FullName}' declares mutable static state."));
        }

        var hasLegacyMetadata = staticFields.Any(static field => field.FieldType == typeof(ToolRegistrationMetadata))
            || hierarchy.SelectMany(static type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Any(static method => string.Equals(method.Name, "Register", StringComparison.Ordinal));

        if (hasLegacyMetadata)
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.LegacyRegistration,
                $"Plugin handler '{handlerType.FullName}' declares legacy static registration metadata."));
        }

        return diagnostics;
    }

    private static DiagnosticInfo CreateDiagnostic(string id, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = ContractDiagnosticSeverity.Warning,
            Message = message,
        };
    }

    private static IEnumerable<Type> GetTypeHierarchy(Type handlerType)
    {
        for (var type = handlerType; type is not null; type = type.BaseType)
        {
            yield return type;
        }
    }
}
