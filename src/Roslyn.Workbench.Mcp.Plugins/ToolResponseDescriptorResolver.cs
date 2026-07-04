using System.Reflection;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Resolves the externally published response shape for registered tools.
/// </summary>
public static class ToolResponseDescriptorResolver
{
    private static readonly HashSet<string> _directToolNames =
    [
        "server-status",
        "workspace-open",
        "workspace-list",
        "workspace-close",
        "workspace-status",
        "workspace-reload",
        "transaction-start",
        "transaction-preview",
        "transaction-history",
        "transaction-commit",
        "transaction-rollback",
    ];

    /// <summary>
    /// Resolves the published response shape for a plugin tool.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="kind">The tool kind.</param>
    /// <param name="responseType">The successful response payload type.</param>
    /// <returns>The resolved response descriptor.</returns>
    public static ToolResponseDescriptor Resolve(string toolName, ToolKind kind, Type responseType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(responseType);

        if (responseType == typeof(MutationData))
        {
            return new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.Mutation,
            };
        }

        if (string.Equals(toolName, "list-code-actions", StringComparison.Ordinal) && responseType == typeof(CodeActionListData))
        {
            return new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.CodeActionList,
                CollectionPropertyName = nameof(CodeActionListData.Actions),
            };
        }

        if (kind == ToolKind.Query && TryResolveCollectionProperty(responseType, out var collectionPropertyName))
        {
            return new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.Collection,
                CollectionPropertyName = collectionPropertyName,
            };
        }

        return new ToolResponseDescriptor
        {
            Kind = ToolResponseShapeKind.Singleton,
        };
    }

    /// <summary>
    /// Resolves the published response shape for a server-owned tool.
    /// </summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="responseType">The successful response payload type.</param>
    /// <returns>The resolved response descriptor.</returns>
    public static ToolResponseDescriptor ResolveServer(string toolName, Type responseType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(responseType);

        if (_directToolNames.Contains(toolName))
        {
            return new ToolResponseDescriptor
            {
                Kind = ToolResponseShapeKind.Direct,
            };
        }

        return Resolve(toolName, ToolKind.Query, responseType);
    }

    private static bool TryResolveCollectionProperty(Type responseType, out string? collectionPropertyName)
    {
        var collectionProperties = responseType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead && IsCollectionProperty(property.PropertyType))
            .ToArray();

        if (collectionProperties.Length == 1
            && responseType.GetProperty("HasMore", BindingFlags.Instance | BindingFlags.Public) is not null
            && responseType.GetProperty("ReturnedCount", BindingFlags.Instance | BindingFlags.Public) is not null)
        {
            collectionPropertyName = collectionProperties[0].Name;
            return true;
        }

        collectionPropertyName = null;
        return false;
    }

    private static bool IsCollectionProperty(Type propertyType)
    {
        if (propertyType == typeof(string))
        {
            return false;
        }

        if (!propertyType.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = propertyType.GetGenericTypeDefinition();

        return genericTypeDefinition == typeof(IReadOnlyList<>)
            || genericTypeDefinition == typeof(IReadOnlyCollection<>)
            || genericTypeDefinition == typeof(IList<>)
            || genericTypeDefinition == typeof(List<>)
            || genericTypeDefinition == typeof(IEnumerable<>);
    }
}
