using System.Reflection;
using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Protocol;

internal static class QueryResponseContract
{
    public static bool TryGetSingletonValueType(Type responseType, out Type? valueType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        if (responseType == typeof(DescribeCodeActionData))
        {
            valueType = responseType;
            return true;
        }

        if (responseType.IsGenericType
            && responseType.GetGenericTypeDefinition() == typeof(QueryResponse<>))
        {
            valueType = responseType.GetGenericArguments()[0];
            return true;
        }

        valueType = null;
        return false;
    }

    public static bool TryGetCollectionItemType(Type responseType, out Type? itemType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        if (responseType.IsGenericType
            && responseType.GetGenericTypeDefinition() == typeof(CollectionResponse<>))
        {
            itemType = responseType.GetGenericArguments()[0];
            return true;
        }

        var attribute = responseType.GetCustomAttribute<PublishedCollectionResponseAttribute>();
        if (attribute is not null)
        {
            var collectionProperty = responseType.GetProperty(attribute.CollectionPropertyName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"Published collection property '{attribute.CollectionPropertyName}' was not found on '{responseType.FullName}'.");
            itemType = collectionProperty.PropertyType.GetGenericArguments()[0];
            return true;
        }

        itemType = null;
        return false;
    }

    public static PublishedCollectionResponseAttribute? GetCollectionAttribute(Type responseType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        return responseType.GetCustomAttribute<PublishedCollectionResponseAttribute>();
    }

    public static bool IsSupportedQueryResponseType(Type responseType)
    {
        ArgumentNullException.ThrowIfNull(responseType);

        return responseType == typeof(CodeActionListData)
            || TryGetSingletonValueType(responseType, out _)
            || TryGetCollectionItemType(responseType, out _);
    }
}
