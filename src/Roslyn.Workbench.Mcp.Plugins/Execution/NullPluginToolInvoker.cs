using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class NullPluginToolInvoker : IPluginToolInvoker
{
    public static readonly NullPluginToolInvoker Instance = new();

    private NullPluginToolInvoker()
    {
    }

    public ValueTask<PluginExecutionResultBox> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        _ = tool;
        _ = arguments;
        _ = contextFactory;
        _ = cancellationToken;

        throw new InvalidOperationException("RegisteredTool does not have an invocation delegate.");
    }
}
