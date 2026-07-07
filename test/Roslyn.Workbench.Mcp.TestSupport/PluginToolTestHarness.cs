using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class PluginToolTestHarness
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        IToolExecutionContextFactory contextFactory,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        var pluginTool = registry.GetRegisteredPluginTool(toolName);
        var result = await pluginTool.Runtime.InvokeAsync(arguments, contextFactory, CancellationToken.None);

        if (result.IsError != !expectProtocolSuccess)
        {
            throw new InvalidOperationException(
                $"Expected protocol success to be '{expectProtocolSuccess}', but 'IsError' was '{result.IsError}'.");
        }

        return DeserializeToolResult<TResponse>(pluginTool.Tool, result.StructuredContent!.Value, toolName);
    }

    public static ToolResult<TResponse> DeserializeToolResult<TResponse>(
        RegisteredTool registeredTool,
        JsonElement payload,
        string toolName)
    {
        ArgumentNullException.ThrowIfNull(registeredTool);

        if (payload.TryGetProperty("outcome", out _))
        {
            return JsonSerializer.Deserialize<ToolResult<TResponse>>(payload.GetRawText(), SerializerOptions)!;
        }

        if (!payload.GetProperty("ok").GetBoolean())
        {
            return ToolResult<TResponse>.Rejected(
                JsonSerializer.Deserialize<ToolError>(payload.GetProperty("error").GetRawText(), SerializerOptions)!,
                payload.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<RequiredAction>(nextElement.GetRawText(), SerializerOptions)
                    : null);
        }

        var data = DeserializeSuccessData<TResponse>(payload, toolName);

        var transactionRevision = data is MutationData mutationData
            ? mutationData.Transaction?.Revision
            : null;

        return ToolResult<TResponse>.Succeeded(data, transactionRevision: transactionRevision);
    }

    private static TResponse DeserializeSuccessData<TResponse>(JsonElement payload, string toolName)
    {
        if (typeof(TResponse) == typeof(MutationData))
        {
            return (TResponse)(object)DeserializeMutationData(payload, toolName);
        }

        if (typeof(TResponse) == typeof(CodeActionListData))
        {
            return (TResponse)(object)DeserializeCodeActionListData(payload);
        }

        if (payload.TryGetProperty("value", out var valueElement))
        {
            return JsonSerializer.Deserialize<TResponse>(valueElement.GetRawText(), SerializerOptions)!;
        }

        if (payload.TryGetProperty("items", out _))
        {
            return DeserializeCollectionData<TResponse>(payload);
        }

        return JsonSerializer.Deserialize<TResponse>(payload.GetRawText(), SerializerOptions)!;
    }

    private static TResponse DeserializeCollectionData<TResponse>(JsonElement payload)
    {
        var collectionAttribute = typeof(TResponse)
            .GetCustomAttributes(typeof(PublishedCollectionResponseAttribute), inherit: false)
            .OfType<PublishedCollectionResponseAttribute>()
            .SingleOrDefault();

        if (collectionAttribute is null)
        {
            return JsonSerializer.Deserialize<TResponse>(payload.GetRawText(), SerializerOptions)!;
        }

        var node = JsonNode.Parse(payload.GetRawText())!.AsObject();
        var itemsNode = node["items"]?.DeepClone();
        var hasMoreNode = node["hasMore"]?.DeepClone();
        var truncatedByNode = node["truncatedBy"]?.DeepClone();

        node.Remove("ok");
        node.Remove("items");
        node.Remove("hasMore");
        node.Remove("truncatedBy");
        node[JsonNamingPolicy.CamelCase.ConvertName(collectionAttribute.CollectionPropertyName)] = itemsNode;
        node["hasMore"] = hasMoreNode;
        node["returnedCount"] = itemsNode is JsonArray itemsArray ? itemsArray.Count : 0;

        if (truncatedByNode is not null && collectionAttribute.TruncationPropertyName is not null)
        {
            node[JsonNamingPolicy.CamelCase.ConvertName(collectionAttribute.TruncationPropertyName)] = truncatedByNode;
        }

        return node.Deserialize<TResponse>(SerializerOptions)!;
    }

    private static MutationData DeserializeMutationData(JsonElement payload, string toolName)
    {
        return new MutationData
        {
            Operation = toolName,
            Summary = payload.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
                ? summaryElement.GetString() ?? string.Empty
                : string.Empty,
            Transaction = payload.TryGetProperty("transaction", out var transactionElement)
                ? new TransactionInfo
                {
                    Revision = transactionElement.GetProperty("revision").GetInt32(),
                }
                : null,
        };
    }

    private static CodeActionListData DeserializeCodeActionListData(JsonElement payload)
    {
        var items = JsonSerializer.Deserialize<IReadOnlyList<CodeActionListItem>>(payload.GetProperty("items").GetRawText(), SerializerOptions) ?? [];

        return new CodeActionListData
        {
            Actions = items.Select(static item => new CodeActionInfo
            {
                ActionId = item.ActionId,
                Title = item.Title,
                ProviderId = item.ProviderId,
                Kind = item.Kind,
                ExecutionMode = item.ExecutionMode,
                ExecutorTool = item.ExecutorTool,
                DescribeTool = item.DescribeTool,
                UnsupportedReasonCode = item.UnsupportedReasonCode,
            }).ToArray(),
            ReturnedCount = items.Count,
            HasMore = payload.GetProperty("hasMore").GetBoolean(),
            TruncationReasons = payload.TryGetProperty("truncatedBy", out var truncatedByElement) && truncatedByElement.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<IReadOnlyList<CollectionTruncation>>(truncatedByElement.GetRawText(), SerializerOptions)
                : null,
        };
    }
}
