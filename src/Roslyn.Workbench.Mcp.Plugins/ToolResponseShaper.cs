using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Shapes normalized tool execution results into the published MCP structured-content payloads.
/// </summary>
public static class ToolResponseShaper
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Shapes one normalized result into the externally published structured-content payload.
    /// </summary>
    /// <param name="descriptor">The resolved response descriptor.</param>
    /// <param name="publishedResponseType">The published successful response payload type.</param>
    /// <param name="result">The normalized tool result.</param>
    /// <returns>The structured-content payload.</returns>
    public static JsonElement Shape(ToolResponseDescriptor descriptor, Type publishedResponseType, PluginExecutionResultBox result)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(publishedResponseType);
        ArgumentNullException.ThrowIfNull(result);

        if (IsFailure(result.Outcome))
        {
            return JsonSerializer.SerializeToElement(CreateFailureResponse(result), _serializerOptions);
        }

        ValidatePublishedResponseData(descriptor, publishedResponseType, result);

        return descriptor.Kind switch
        {
            ToolResponseShapeKind.Direct => JsonSerializer.SerializeToElement(CreateDirectSuccessResponse(publishedResponseType, result), _serializerOptions),
            ToolResponseShapeKind.Singleton => JsonSerializer.SerializeToElement(CreateSingletonSuccessResponse(publishedResponseType, result), _serializerOptions),
            ToolResponseShapeKind.Collection => JsonSerializer.SerializeToElement(CreateCollectionSuccessResponse(descriptor, publishedResponseType, result), _serializerOptions),
            ToolResponseShapeKind.Mutation => JsonSerializer.SerializeToElement(CreateMutationSuccessResponse(result), _serializerOptions),
            ToolResponseShapeKind.CodeActionList => JsonSerializer.SerializeToElement(CreateCodeActionListSuccessResponse(result), _serializerOptions),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{descriptor.Kind}'."),
        };
    }

    private static JsonObject CreateFailureResponse(PluginExecutionResultBox result)
    {
        var payload = new JsonObject
        {
            ["ok"] = false,
            ["error"] = result.Error is null
                ? null
                : JsonSerializer.SerializeToNode(result.Error, typeof(ToolError), _serializerOptions),
        };

        if (result.RequiredAction is not null)
        {
            payload["next"] = JsonSerializer.SerializeToNode(result.RequiredAction, typeof(RequiredAction), _serializerOptions);
        }

        return payload;
    }

    private static JsonObject CreateDirectSuccessResponse(Type responseType, PluginExecutionResultBox result)
    {
        var payload = new JsonObject
        {
            ["ok"] = true,
        };

        if (result.Data is null)
        {
            return payload;
        }

        var serialized = SerializeObject(result.Data, responseType);
        foreach (var property in serialized)
        {
            payload[property.Key] = property.Value?.DeepClone();
        }

        return payload;
    }

    private static JsonObject CreateSingletonSuccessResponse(Type responseType, PluginExecutionResultBox result)
    {
        return new JsonObject
        {
            ["ok"] = true,
            ["value"] = result.Data is null
                ? null
                : JsonSerializer.SerializeToNode(result.Data, responseType, _serializerOptions),
        };
    }

    private static JsonObject CreateCollectionSuccessResponse(ToolResponseDescriptor descriptor, Type responseType, PluginExecutionResultBox result)
    {
        var serialized = result.Data is null
            ? new JsonObject()
            : SerializeObject(result.Data, responseType);
        var collectionPropertyName = JsonNamingPolicy.CamelCase.ConvertName(descriptor.CollectionPropertyName!);
        var items = serialized.TryGetPropertyValue(collectionPropertyName, out var itemsNode)
            ? itemsNode?.DeepClone()
            : new JsonArray();
        var hasMore = serialized.TryGetPropertyValue("hasMore", out var hasMoreNode) ? hasMoreNode?.DeepClone() : JsonValue.Create(false);

        serialized.Remove(collectionPropertyName);
        serialized.Remove("returnedCount");
        serialized.Remove("hasMore");

        var payload = new JsonObject
        {
            ["ok"] = true,
            ["items"] = items,
            ["hasMore"] = hasMore,
        };

        if (serialized.TryGetPropertyValue("truncationReasons", out var truncationNode))
        {
            serialized.Remove("truncationReasons");
            if (truncationNode is JsonArray truncationArray && truncationArray.Count > 0)
            {
                payload["truncatedBy"] = truncationArray.DeepClone();
            }
        }

        foreach (var property in serialized)
        {
            payload[property.Key] = property.Value?.DeepClone();
        }

        return payload;
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
            ["items"] = JsonSerializer.SerializeToNode(items, typeof(CodeActionListItem[]), _serializerOptions),
            ["hasMore"] = data?.HasMore ?? false,
        };

        if (data?.TruncationReasons is { Count: > 0 } truncationReasons)
        {
            payload["truncatedBy"] = JsonSerializer.SerializeToNode(truncationReasons, typeof(IReadOnlyList<CollectionTruncation>), _serializerOptions);
        }

        return payload;
    }

    private static JsonObject SerializeObject(object value, Type valueType)
    {
        var serialized = JsonSerializer.SerializeToNode(value, valueType, _serializerOptions);

        if (serialized is JsonObject objectNode)
        {
            return objectNode;
        }

        throw new InvalidOperationException($"Published response type '{valueType.FullName}' must serialize as a JSON object.");
    }

    private static bool IsFailure(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }

    private static void ValidatePublishedResponseData(ToolResponseDescriptor descriptor, Type publishedResponseType, PluginExecutionResultBox result)
    {
        if (result.Data is null)
        {
            return;
        }

        var expectedType = descriptor.Kind switch
        {
            ToolResponseShapeKind.Mutation => typeof(MutationData),
            ToolResponseShapeKind.CodeActionList => typeof(CodeActionListData),
            _ => publishedResponseType,
        };

        if (!expectedType.IsInstanceOfType(result.Data))
        {
            throw new InvalidOperationException(
                $"Published response data type mismatch. Expected '{expectedType.FullName}' but got '{result.Data.GetType().FullName}'.");
        }
    }
}
