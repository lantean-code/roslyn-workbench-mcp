using System.Reflection;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal static class QueryResponseContractInspector
{
    public static IReadOnlyList<DiagnosticInfo> Inspect(RegisteredTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (tool.Kind != ToolKind.Query
            || string.Equals(tool.Metadata.Name, "list-code-actions", StringComparison.Ordinal))
        {
            return [];
        }

        var offendingProperties = tool.ResponseType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => IsRawTopLevelCollection(property.PropertyType))
            .Select(static property => property.Name)
            .ToArray();

        if (offendingProperties.Length == 0)
        {
            return [];
        }

        return
        [
            new DiagnosticInfo
            {
                Id = "QueryResponseContract",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Tool '{tool.Metadata.Name}' publishes unbounded top-level collections on response '{tool.ResponseType.Name}': {string.Join(", ", offendingProperties)}. Prefer BoundedCollection<TItem> for agent-facing top-level collections.",
            },
        ];
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
