using System.Text.Json;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed class PluginMcpServerTool : McpServerTool
{
    private readonly RegisteredTool _registeredTool;
    private readonly ToolExecutor _toolExecutor;
    private readonly Tool _protocolTool;

    public PluginMcpServerTool(RegisteredTool registeredTool, ToolExecutor toolExecutor)
    {
        _registeredTool = registeredTool;
        _toolExecutor = toolExecutor;
        var description = string.IsNullOrWhiteSpace(registeredTool.Metadata.ResultSummary)
            ? registeredTool.Metadata.Description
            : $"{registeredTool.Metadata.Description} Result: {registeredTool.Metadata.ResultSummary}";
        _protocolTool = new Tool
        {
            Name = registeredTool.Metadata.Name,
            Title = registeredTool.Metadata.Title,
            Description = description,
            InputSchema = registeredTool.InputSchema,
            OutputSchema = registeredTool.OutputSchema,
            Annotations = registeredTool.Annotations,
        };
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> requestContext, CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments
            ?? (IDictionary<string, JsonElement>)new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return _toolExecutor.ExecuteAsync(_registeredTool, arguments, cancellationToken);
    }
}
