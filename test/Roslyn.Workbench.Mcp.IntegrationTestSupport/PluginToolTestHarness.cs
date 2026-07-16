using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Protocol;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class PluginToolTestHarness
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        IToolExecutionContextFactory contextFactory,
        PluginToolCatalogue catalogue,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        var pluginTool = GetTool(catalogue.Tools, toolName);
        var result = await InvokeRawAsync(contextFactory, catalogue, toolName, arguments);

        if (result.IsError != !expectProtocolSuccess)
        {
            throw new InvalidOperationException(
                $"Expected protocol success to be '{expectProtocolSuccess}', but 'IsError' was '{result.IsError}'.");
        }

        return DeserializeToolResult<TResponse>(result.StructuredContent!.Value, toolName);
    }

    public static async Task<CallToolResult> InvokeRawAsync(
        IToolExecutionContextFactory contextFactory,
        PluginToolCatalogue catalogue,
        string toolName,
        IDictionary<string, JsonElement> arguments)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        var pluginTool = GetTool(catalogue.Tools, toolName);
        var serverTool = pluginTool.Accept(new PluginMcpServerToolFactory(
            contextFactory,
            CreateProtocolFactory()));
        return await serverTool.InvokeArgumentsAsync(arguments, CancellationToken.None);
    }

    public static ToolResult<TResponse> DeserializeToolResult<TResponse>(
        JsonElement payload,
        string toolName)
    {

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

    private static IMcpToolProtocolFactory CreateProtocolFactory()
    {
        return new McpToolProtocolFactory(new ToolSchemaFactory(new McpSdkSchemaProvider()));
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

    private static IRegisteredPluginTool GetTool(IReadOnlyList<IRegisteredPluginTool> catalogue, string toolName)
    {
        return catalogue.Single(tool => string.Equals(tool.Tool.Metadata.Name, toolName, StringComparison.Ordinal));
    }
}
