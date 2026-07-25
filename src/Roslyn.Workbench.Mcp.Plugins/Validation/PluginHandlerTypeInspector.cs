using System.Composition;
using System.Reflection;
using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal sealed class PluginHandlerTypeInspector : IPluginHandlerTypeInspector
{
    public IReadOnlyList<DiagnosticInfo> Inspect(Type handlerType)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var isDisposable = typeof(IDisposable).IsAssignableFrom(handlerType);
        var isAsyncDisposable = typeof(IAsyncDisposable).IsAssignableFrom(handlerType);
        if (isDisposable || isAsyncDisposable)
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerLifetime,
                $"Plugin handler '{handlerType.FullName}' must not own a disposable lifetime.");

            diagnostics.Add(diagnostic);
        }

        if (HasMefImports(handlerType))
        {
            var diagnostic = CreateDiagnostic(
                PluginDiagnosticIds.HandlerComposition,
                $"Plugin handler '{handlerType.FullName}' must not declare MEF imports.");

            diagnostics.Add(diagnostic);
        }

        return diagnostics;
    }

    private static DiagnosticInfo CreateDiagnostic(string id, string message)
    {
        var diagnostic = new DiagnosticInfo
        {
            Id = id,
            Severity = ContractDiagnosticSeverity.Error,
            Message = message,
        };

        return diagnostic;
    }

    private static bool HasMefImports(Type handlerType)
    {
        foreach (var type in GetTypeHierarchy(handlerType))
        {
            var members = type.GetMembers(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (ContainsImportedMember(members))
            {
                return true;
            }

            var constructors = type.GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            if (ContainsImportedParameter(constructors))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsImportedMember(MemberInfo[] members)
    {
        foreach (var member in members)
        {
            if (HasImportAttribute(member.CustomAttributes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsImportedParameter(ConstructorInfo[] constructors)
    {
        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (HasImportAttribute(parameter.CustomAttributes))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasImportAttribute(IEnumerable<CustomAttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            var attributeType = attribute.AttributeType;
            if (attributeType == typeof(ImportAttribute))
            {
                return true;
            }

            if (attributeType == typeof(ImportManyAttribute))
            {
                return true;
            }

            if (attributeType == typeof(ImportingConstructorAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Type> GetTypeHierarchy(Type handlerType)
    {
        for (var type = handlerType; type is not null; type = type.BaseType)
        {
            yield return type;
        }
    }
}
