using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal abstract class McpServerToolBase : McpServerTool
{
    private readonly Tool _protocolTool;

    protected McpServerToolBase(Tool protocolTool)
    {
        _protocolTool = protocolTool;
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        return await InvokeArgumentsAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<CallToolResult> InvokeArgumentsAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        return await InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    protected abstract ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken);

    protected static CallToolResult CreateStructuredResult(JsonElement content, bool isError)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = content,
            IsError = isError,
        };
    }
}
