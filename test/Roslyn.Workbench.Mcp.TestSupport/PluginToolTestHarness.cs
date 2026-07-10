using System.Text.Json;
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
        var result = await pluginTool.ExecutionAdapter.InvokeAsync(arguments, contextFactory, CancellationToken.None);

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

        return JsonSerializer.Deserialize<TResponse>(payload.GetProperty("data").GetRawText(), SerializerOptions)!;
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
}
