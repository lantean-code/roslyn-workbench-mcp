using System.Composition;
using System.Reflection;
using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Contracts.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins;

internal sealed class PluginHandlerTypeInspector : IPluginHandlerTypeInspector
{
    public IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType)
    {
        var diagnostics = new List<DiagnosticInfo>();
        if (typeof(IDisposable).IsAssignableFrom(handlerType) || typeof(IAsyncDisposable).IsAssignableFrom(handlerType))
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.HandlerLifetime,
                $"Plugin handler '{handlerType.FullName}' must not own a disposable lifetime."));
        }

        if (HasMefImports(handlerType))
        {
            diagnostics.Add(CreateDiagnostic(
                PluginDiagnosticIds.HandlerComposition,
                $"Plugin handler '{handlerType.FullName}' must not declare MEF imports."));
        }

        return diagnostics;
    }

    private static DiagnosticInfo CreateDiagnostic(string id, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = ContractDiagnosticSeverity.Error,
            Message = message,
        };
    }

    private static bool HasMefImports(Type handlerType)
    {
        foreach (var type in GetTypeHierarchy(handlerType))
        {
            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (members.Any(member => HasImportAttribute(member.CustomAttributes))
                || type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SelectMany(static constructor => constructor.GetParameters())
                    .Any(parameter => HasImportAttribute(parameter.CustomAttributes)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasImportAttribute(IEnumerable<CustomAttributeData> attributes)
    {
        return attributes.Any(attribute => attribute.AttributeType == typeof(ImportAttribute)
            || attribute.AttributeType == typeof(ImportManyAttribute)
            || attribute.AttributeType == typeof(ImportingConstructorAttribute));
    }

    private static IEnumerable<Type> GetTypeHierarchy(Type handlerType)
    {
        for (var type = handlerType; type is not null; type = type.BaseType)
        {
            yield return type;
        }
    }
}
