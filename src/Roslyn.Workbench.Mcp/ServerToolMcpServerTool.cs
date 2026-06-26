using System.Text.Json;
using System.Text.Json.Nodes;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Roslyn.Workbench.Mcp.Contracts.Results;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed class ServerToolMcpServerTool<TRequest, TResponse> : McpServerTool
    where TRequest : class
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Func<TRequest, RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<ToolResult<TResponse>>> _handler;
    private readonly Tool _protocolTool;

    public ServerToolMcpServerTool(
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        Func<TRequest, RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<ToolResult<TResponse>>> handler)
    {
        _handler = handler;
        _protocolTool = new Tool
        {
            Name = name,
            Title = title,
            Description = description,
            InputSchema = ToolSchemaFactory.CreateInputSchema<TRequest>(),
            OutputSchema = ToolSchemaFactory.CreateToolResultSchema<TResponse>(),
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
                StructuredContent = JsonSerializer.SerializeToElement(result, _serializerOptions),
                IsError = result.Outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var result = ToolResult<TResponse>.Faulted(new ToolError
            {
                Code = "UnhandledException",
                Message = "Tool execution failed.",
                CorrelationId = Guid.NewGuid().ToString("n"),
            });

            return new CallToolResult
            {
                Content = [],
                StructuredContent = JsonSerializer.SerializeToElement(result, _serializerOptions),
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
