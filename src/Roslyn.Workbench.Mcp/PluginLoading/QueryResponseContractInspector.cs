using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal static class QueryResponseContractInspector
{
    public static IReadOnlyList<DiagnosticInfo> Inspect(RegisteredTool tool)
    {

        if (tool.Kind != ToolKind.Query
            || string.Equals(tool.Metadata.Name, "list-code-actions", StringComparison.Ordinal))
        {
            return [];
        }

        var offendingProperties = new List<string>();
        foreach (var property in tool.ResponseType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsRawTopLevelCollection(property.PropertyType))
            {
                offendingProperties.Add(property.Name);
            }
        }

        if (offendingProperties.Count == 0)
        {
            return [];
        }

        var diagnostic = new DiagnosticInfo
        {
            Id = "QueryResponseContract",
            Severity = DiagnosticSeverity.Warning,
            Message = $"Tool '{tool.Metadata.Name}' publishes unbounded top-level collections on response '{tool.ResponseType.Name}': {string.Join(", ", offendingProperties)}. Prefer BoundedCollection<TItem> for agent-facing top-level collections.",
        };

        return [diagnostic];
    }

    private static bool IsRawTopLevelCollection(Type propertyType)
    {
        if (propertyType == typeof(string))
        {
            return false;
        }

        if (propertyType.IsGenericType
            && propertyType.GetGenericTypeDefinition() == typeof(BoundedCollection<>))
        {
            return false;
        }

        if (propertyType.IsArray)
        {
            return true;
        }

        if (!propertyType.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = propertyType.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(IReadOnlyList<>)
            || genericTypeDefinition == typeof(IReadOnlyCollection<>)
            || genericTypeDefinition == typeof(IEnumerable<>)
            || genericTypeDefinition == typeof(ICollection<>)
            || genericTypeDefinition == typeof(IList<>)
            || genericTypeDefinition == typeof(List<>);
    }
}
