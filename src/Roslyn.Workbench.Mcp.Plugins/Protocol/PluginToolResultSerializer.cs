using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Protocol;

internal static class PluginToolResultSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Serialize(
        ToolKind kind,
        Type responseType,
        PluginExecutionResultBox result)
    {
        ArgumentNullException.ThrowIfNull(responseType);
        ArgumentNullException.ThrowIfNull(result);

        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        if (kind == ToolKind.Mutation)
        {
            ValidatePublishedResponseData(typeof(MutationData), result);
            return JsonSerializer.SerializeToElement(CreateMutationSuccessResponse(result), SerializerOptions);
        }

        if (responseType == typeof(CodeActionListData))
        {
            ValidatePublishedResponseData(typeof(CodeActionListData), result);
            return JsonSerializer.SerializeToElement(CreateCodeActionListSuccessResponse(result), SerializerOptions);
        }

        if (responseType == typeof(DescribeCodeActionData))
        {
            ValidatePublishedResponseData(typeof(DescribeCodeActionData), result);
            return ToolStructuredResultSerializer.CreateSingletonSuccess(result.Data, typeof(DescribeCodeActionData));
        }

        if (QueryResponseContract.TryGetCollectionItemType(responseType, out _))
        {
            ValidatePublishedResponseData(responseType, result);
            return JsonSerializer.SerializeToElement(CreateCollectionSuccessResponse(responseType, result), SerializerOptions);
        }

        if (QueryResponseContract.TryGetSingletonValueType(responseType, out var valueType))
        {
            ValidatePublishedResponseData(responseType, result);
            return CreateSingletonSuccessResponse(
                responseType,
                valueType ?? throw new InvalidOperationException($"Singleton response type '{responseType.FullName}' did not resolve a value type."),
                result);
        }

        throw new InvalidOperationException($"Unsupported query response type '{responseType.FullName}'.");
    }

    private static JsonElement CreateSingletonSuccessResponse(
        Type responseType,
        Type valueType,
        PluginExecutionResultBox result)
    {
        if (result.Data is null)
        {
            return ToolStructuredResultSerializer.CreateSingletonSuccess(data: null, dataType: valueType);
        }

        var serialized = SerializeObject(result.Data, responseType);
        var valueNode = serialized["value"]?.DeepClone();

        return ToolStructuredResultSerializer.CreateSingletonSuccess(
            data: valueNode is null
                ? null
                : JsonSerializer.Deserialize(valueNode.ToJsonString(), valueType, SerializerOptions),
            dataType: valueType);
    }

    private static JsonObject CreateCollectionSuccessResponse(Type responseType, PluginExecutionResultBox result)
    {
        if (result.Data is null)
        {
            return new JsonObject
            {
                ["ok"] = true,
                ["items"] = new JsonArray(),
                ["hasMore"] = false,
            };
        }

        var serialized = SerializeObject(result.Data, result.Data.GetType());
        var attribute = QueryResponseContract.GetCollectionAttribute(responseType);
        if (attribute is not null)
        {
            var collectionPropertyName = JsonNamingPolicy.CamelCase.ConvertName(attribute.CollectionPropertyName);
            var payload = new JsonObject
            {
                ["ok"] = true,
                ["items"] = serialized[collectionPropertyName]?.DeepClone() ?? new JsonArray(),
                ["hasMore"] = serialized["hasMore"]?.DeepClone() ?? JsonValue.Create(false),
            };
            serialized.Remove(collectionPropertyName);
            serialized.Remove("returnedCount");
            serialized.Remove("hasMore");

            if (!string.IsNullOrWhiteSpace(attribute.TruncationPropertyName))
            {
                serialized.Remove(JsonNamingPolicy.CamelCase.ConvertName(attribute.TruncationPropertyName));
            }

            foreach (var property in serialized)
            {
                payload[property.Key] = property.Value?.DeepClone();
            }

            return payload;
        }

        var standardPayload = new JsonObject
        {
            ["ok"] = true,
            ["items"] = serialized["items"]?.DeepClone() ?? new JsonArray(),
            ["hasMore"] = serialized["hasMore"]?.DeepClone() ?? JsonValue.Create(false),
        };

        if (serialized["truncatedBy"] is JsonArray truncationArray && truncationArray.Count > 0)
        {
            standardPayload["truncatedBy"] = truncationArray.DeepClone();
        }

        return standardPayload;
    }

    private static JsonObject CreateMutationSuccessResponse(PluginExecutionResultBox result)
    {
        var payload = new JsonObject
        {
            ["ok"] = true,
        };

        if (result.Outcome == ToolOutcome.NoChange || result.Data is not MutationData mutation)
        {
            payload["staged"] = false;
            return payload;
        }

        payload["staged"] = true;
        payload["summary"] = mutation.Summary;

        if (mutation.Transaction?.Revision is int revision)
        {
            payload["transaction"] = new JsonObject
            {
                ["revision"] = revision,
            };
        }

        return payload;
    }

    private static JsonObject CreateCodeActionListSuccessResponse(PluginExecutionResultBox result)
    {
        var data = result.Data as CodeActionListData;
        var items = data?.Actions.Select(static action => new CodeActionListItem
        {
            ActionId = action.ActionId,
            Title = action.Title,
            ProviderId = action.ProviderId,
            Kind = action.Kind,
            ExecutionMode = action.ExecutionMode,
            ExecutorTool = action.ExecutorTool,
            DescribeTool = action.DescribeTool,
            UnsupportedReasonCode = action.UnsupportedReasonCode,
        }).ToArray() ?? [];

        var payload = new JsonObject
        {
            ["ok"] = true,
            ["items"] = JsonSerializer.SerializeToNode(items, SerializerOptions),
            ["hasMore"] = data?.HasMore ?? false,
        };

        if (data?.TruncationReasons is { Count: > 0 } truncationReasons)
        {
            payload["truncatedBy"] = JsonSerializer.SerializeToNode(truncationReasons, SerializerOptions);
        }

        return payload;
    }

    private static JsonObject SerializeObject(object value, Type valueType)
    {
        var serialized = JsonSerializer.SerializeToNode(value, valueType, SerializerOptions);

        if (serialized is JsonObject objectNode)
        {
            return objectNode;
        }

        throw new InvalidOperationException($"Published response type '{valueType.FullName}' must serialize as a JSON object.");
    }

    private static void ValidatePublishedResponseData(Type expectedType, PluginExecutionResultBox result)
    {
        if (result.Data is null)
        {
            return;
        }

        if (!expectedType.IsInstanceOfType(result.Data))
        {
            throw new InvalidOperationException(
                $"Published response data type mismatch. Expected '{expectedType.FullName}' but got '{result.Data.GetType().FullName}'.");
        }
    }

    private static bool IsFailure(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }

}
