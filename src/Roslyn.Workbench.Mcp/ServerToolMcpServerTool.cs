using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed class ServerToolMcpServerTool<TRequest, TResponse> : McpServerTool
    where TRequest : class
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Func<TRequest, RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<ToolResult<TResponse>>> _handler;
    private readonly ToolResponseDescriptor _responseDescriptor;
    private readonly Tool _protocolTool;

    public ServerToolMcpServerTool(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        ToolOutputSchemaMode outputSchemaMode,
        string? resultSummary,
        Func<TRequest, RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<ToolResult<TResponse>>> handler)
    {
        _handler = handler;
        _responseDescriptor = ToolResponseDescriptorResolver.ResolveServer(name, typeof(TResponse));
        var publishedDescription = string.IsNullOrWhiteSpace(resultSummary)
            ? description
            : $"{description} Result: {resultSummary}";
        _protocolTool = new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
                ? ToolSchemaFactory.CreateOutputSchema(_responseDescriptor, typeof(TResponse))
                : null,
            Annotations = new ToolAnnotations
            {
                Title = title,
                ReadOnlyHint = readOnly,
                IdempotentHint = readOnly,
                OpenWorldHint = false,
                DestructiveHint = destructive,
            },
        };
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> requestContext, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = requestContext.Params.Arguments
                ?? (IDictionary<string, JsonElement>)new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var request = DeserializeRequest(arguments);
            var result = await _handler(request, requestContext, cancellationToken);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = ToolResponseShaper.Shape(_responseDescriptor, typeof(TResponse), PluginExecutionResultBox.From(result)),
                IsError = result.Outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new CallToolResult
            {
                Content = [],
                StructuredContent = ToolResponseShaper.Shape(_responseDescriptor, typeof(TResponse), PluginExecutionResultBox.CreateUnhandledException()),
                IsError = true,
            };
        }
    }

    private static TRequest DeserializeRequest(IDictionary<string, JsonElement> arguments)
    {
        var requestNode = new JsonObject();

        foreach (var pair in arguments)
        {
            requestNode[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
        }

        var request = requestNode.Deserialize<TRequest>(_serializerOptions);

        if (request is null)
        {
            throw new JsonException($"Request payload for '{typeof(TRequest).FullName}' could not be deserialized.");
        }

        return request;
    }
}
