using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class NullPluginToolExecutionAdapter : IPluginToolExecutionAdapter
{
    public static NullPluginToolExecutionAdapter Instance { get; } = new();

    private NullPluginToolExecutionAdapter()
    {
    }

    public ValueTask<CallToolResult> InvokeAsync(
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        throw new InvalidOperationException("Registered plugin tool does not have an execution adapter binding.");
    }
}
