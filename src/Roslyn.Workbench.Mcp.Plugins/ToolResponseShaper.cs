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
        return CreateWriter(descriptor, publishedResponseType)(result);
    }

    internal static Func<PluginExecutionResultBox, JsonElement> CreateWriter(ToolResponseDescriptor descriptor, Type publishedResponseType)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(publishedResponseType);

        return descriptor.Kind switch
        {
            ToolResponseShapeKind.Direct => result => ShapeDirectSuccess(publishedResponseType, result),
            ToolResponseShapeKind.Singleton => result => ShapeSingletonSuccess(publishedResponseType, result),
            ToolResponseShapeKind.Collection => result => ShapeCollectionSuccess(descriptor, publishedResponseType, result),
            ToolResponseShapeKind.Mutation => ShapeMutationSuccess,
            ToolResponseShapeKind.CodeActionList => ShapeCodeActionListSuccess,
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{descriptor.Kind}'."),
        };
    }

    private static JsonElement ShapeDirectSuccess(Type responseType, PluginExecutionResultBox result)
    {
        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        ValidatePublishedResponseData(ToolResponseShapeKind.Direct, responseType, result);
        return ToolStructuredResultSerializer.CreateDirectSuccess(result.Data, responseType);
    }

    private static JsonElement ShapeSingletonSuccess(Type responseType, PluginExecutionResultBox result)
    {
        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        ValidatePublishedResponseData(ToolResponseShapeKind.Singleton, responseType, result);
        return ToolStructuredResultSerializer.CreateSingletonSuccess(result.Data, responseType);
    }

    private static JsonElement ShapeCollectionSuccess(ToolResponseDescriptor descriptor, Type responseType, PluginExecutionResultBox result)
    {
        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        ValidatePublishedResponseData(descriptor.Kind, responseType, result);
        return JsonSerializer.SerializeToElement(CreateCollectionSuccessResponse(descriptor, responseType, result), _serializerOptions);
    }

    private static JsonElement ShapeMutationSuccess(PluginExecutionResultBox result)
    {
        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        ValidatePublishedResponseData(ToolResponseShapeKind.Mutation, typeof(MutationData), result);
        return JsonSerializer.SerializeToElement(CreateMutationSuccessResponse(result), _serializerOptions);
    }

    private static JsonElement ShapeCodeActionListSuccess(PluginExecutionResultBox result)
    {
        if (IsFailure(result.Outcome))
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        ValidatePublishedResponseData(ToolResponseShapeKind.CodeActionList, typeof(CodeActionListData), result);
        return JsonSerializer.SerializeToElement(CreateCodeActionListSuccessResponse(result), _serializerOptions);
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
            ["items"] = JsonSerializer.SerializeToNode(items, _serializerOptions),
            ["hasMore"] = data?.HasMore ?? false,
        };

        if (data?.TruncationReasons is { Count: > 0 } truncationReasons)
        {
            payload["truncatedBy"] = JsonSerializer.SerializeToNode(truncationReasons, _serializerOptions);
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

    private static void ValidatePublishedResponseData(ToolResponseShapeKind shapeKind, Type publishedResponseType, PluginExecutionResultBox result)
    {
        if (result.Data is null)
        {
            return;
        }

        var expectedType = shapeKind switch
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
