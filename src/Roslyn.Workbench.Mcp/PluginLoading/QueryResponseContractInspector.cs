using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal static class QueryResponseContractInspector
{
    private static readonly Type[] _rawCollectionDefinitions =
    [
        typeof(IEnumerable<>),
        typeof(ICollection<>),
        typeof(IList<>),
        typeof(IReadOnlyCollection<>),
        typeof(IReadOnlyList<>),
        typeof(List<>),
        typeof(ISet<>),
        typeof(IReadOnlySet<>),
        typeof(HashSet<>),
        typeof(IDictionary<,>),
        typeof(IReadOnlyDictionary<,>),
        typeof(Dictionary<,>),
        typeof(IAsyncEnumerable<>),
    ];

    public static IReadOnlyList<DiagnosticInfo> Inspect(RegisteredTool tool)
    {
        if (tool.Kind != ToolKind.Query
            || string.Equals(tool.Metadata.Name, "list-code-actions", StringComparison.Ordinal))
        {
            return [];
        }

        var responseType = tool.ResponseType;
        if (IsBoundedCollection(responseType))
        {
            return [];
        }

        if (IsRawCollection(responseType))
        {
            var responseDiagnostic = CreateDiagnostic(
                $"Tool '{tool.Metadata.Name}' publishes unbounded collection response '{responseType.Name}'. Prefer BoundedCollection<TItem> for agent-facing collections.");

            return [responseDiagnostic];
        }

        var offendingProperties = new List<string>();
        foreach (var property in responseType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsRawCollection(property.PropertyType))
            {
                offendingProperties.Add(property.Name);
            }
        }

        if (offendingProperties.Count == 0)
        {
            return [];
        }

        var propertyNames = string.Join(", ", offendingProperties);
        var diagnostic = CreateDiagnostic(
            $"Tool '{tool.Metadata.Name}' publishes unbounded top-level collections on response '{responseType.Name}': {propertyNames}. Prefer BoundedCollection<TItem> for agent-facing top-level collections.");

        return [diagnostic];
    }

    private static DiagnosticInfo CreateDiagnostic(string message)
    {
        var diagnostic = new DiagnosticInfo
        {
            Id = "QueryResponseContract",
            Severity = DiagnosticSeverity.Warning,
            Message = message,
        };

        return diagnostic;
    }

    private static bool IsRawCollection(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (IsBoundedCollection(type))
        {
            return false;
        }

        if (type.IsArray)
        {
            return true;
        }

        if (MatchesRawCollectionDefinition(type))
        {
            return true;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (MatchesRawCollectionDefinition(interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBoundedCollection(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(BoundedCollection<>);
    }

    private static bool MatchesRawCollectionDefinition(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        foreach (var rawCollectionDefinition in _rawCollectionDefinitions)
        {
            if (definition == rawCollectionDefinition)
            {
                return true;
            }
        }

        return false;
    }
}
