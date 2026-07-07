using System.Text.Json;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed class PluginMcpServerTool : McpServerTool
{
    private readonly RegisteredPluginTool _registeredTool;
    private readonly IToolExecutionContextFactory _contextFactory;
    private readonly Tool _protocolTool;

    public PluginMcpServerTool(
        RegisteredPluginTool registeredTool,
        IToolExecutionContextFactory contextFactory)
    {
        _registeredTool = registeredTool;
        _contextFactory = contextFactory;
        var description = string.IsNullOrWhiteSpace(registeredTool.Tool.Metadata.ResultSummary)
            ? registeredTool.Tool.Metadata.Description
            : $"{registeredTool.Tool.Metadata.Description} Result: {registeredTool.Tool.Metadata.ResultSummary}";
        _protocolTool = new Tool
        {
            Name = registeredTool.Tool.Metadata.Name,
            Title = registeredTool.Tool.Metadata.Title,
            Description = description,
            InputSchema = registeredTool.Tool.InputSchema,
            OutputSchema = registeredTool.Tool.OutputSchema,
            Annotations = registeredTool.Tool.Annotations,
        };
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> requestContext, CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return _registeredTool.Runtime.InvokeAsync(arguments, _contextFactory, cancellationToken);
    }
}
