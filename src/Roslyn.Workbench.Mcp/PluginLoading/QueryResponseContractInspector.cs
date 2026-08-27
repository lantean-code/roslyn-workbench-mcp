using System.Diagnostics.CodeAnalysis;
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

    public static string? Inspect(RegisteredTool tool)
    {
        if (tool.Kind != ToolKind.Query
            || string.Equals(tool.Metadata.Name, "list-code-actions", StringComparison.Ordinal))
        {
            return null;
        }

        var responseType = tool.ResponseType;
        if (IsBoundedCollection(responseType))
        {
            return null;
        }

        if (IsRawCollection(responseType))
        {
            return $"Response '{responseType.Name}' is an unbounded collection. Prefer BoundedCollection<TItem> for agent-facing collections.";
        }

        var offendingProperties = new List<string>();
        foreach (var property in responseType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsRawCollection(property.PropertyType)
                && !HasQueryResponseCollectionSuppression(property))
            {
                offendingProperties.Add(property.Name);
            }
        }

        if (offendingProperties.Count == 0)
        {
            return null;
        }

        var propertyNames = string.Join(", ", offendingProperties);
        return $"Response '{responseType.Name}' publishes unbounded top-level collections: {propertyNames}. Prefer BoundedCollection<TItem> for agent-facing top-level collections.";
    }

    private static bool HasQueryResponseCollectionSuppression(PropertyInfo property)
    {
        foreach (var suppression in property.GetCustomAttributes<UnconditionalSuppressMessageAttribute>(inherit: true))
        {
            if (string.Equals(suppression.Category, "RoslynWorkbench.PluginAuthoring", StringComparison.Ordinal)
                && (string.Equals(suppression.CheckId, "RWMCP014", StringComparison.Ordinal)
                    || suppression.CheckId.StartsWith("RWMCP014:", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
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
