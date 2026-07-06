using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal interface IPluginToolInvoker
{
    ValueTask<PluginExecutionResultBox> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken);
}
