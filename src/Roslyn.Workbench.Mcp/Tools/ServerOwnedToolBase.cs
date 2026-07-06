using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Tools;

internal abstract class ServerOwnedToolBase<TRequest, TResponse> : McpServerTool
    where TRequest : class
{
    private readonly Tool _protocolTool;

    protected ServerOwnedToolBase(
        IOptions<StartupOptions> startupOptions,
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary = null)
    {
        var publishedDescription = string.IsNullOrWhiteSpace(resultSummary)
            ? description
            : $"{description} Result: {resultSummary}";
        _protocolTool = new Tool
        {
            Name = name,
            Title = title,
            Description = publishedDescription,
            InputSchema = ToolSchemaBuilder.CreateInputSchema<TRequest>(),
            OutputSchema = startupOptions.Value.ToolOutputSchemaMode == ToolOutputSchemaMode.Full
                ? ToolSchemaBuilder.CreateDirectOutputSchema(typeof(TResponse))
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
            var arguments = requestContext.Params.Arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var request = DeserializeRequest(arguments);
            var result = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = ShapeResult(result),
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
                StructuredContent = CreateUnhandledExceptionResponse(),
                IsError = true,
            };
        }
    }

    protected abstract ValueTask<ToolResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);

    private static TRequest DeserializeRequest(IDictionary<string, JsonElement> arguments)
    {
        return ToolRequestBinder.Deserialize<TRequest>(arguments);
    }

    private static JsonElement ShapeResult(ToolResult<TResponse> result)
    {
        if (result.Outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted)
        {
            return ToolStructuredResultSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        return ToolStructuredResultSerializer.CreateDirectSuccess(result.Data, typeof(TResponse));
    }

    private static JsonElement CreateUnhandledExceptionResponse()
    {
        return ToolStructuredResultSerializer.CreateUnhandledException();
    }
}
