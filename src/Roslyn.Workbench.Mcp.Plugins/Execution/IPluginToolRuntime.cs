using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal interface IPluginToolRuntime
{
    ValueTask<CallToolResult> InvokeAsync(
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken);
}
