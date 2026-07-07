using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class NullPluginToolRuntime : IPluginToolRuntime
{
    public static NullPluginToolRuntime Instance { get; } = new();

    private NullPluginToolRuntime()
    {
    }

    public ValueTask<CallToolResult> InvokeAsync(
        IDictionary<string, JsonElement> arguments,
        IToolExecutionContextFactory contextFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contextFactory);

        throw new InvalidOperationException("Registered plugin tool does not have a runtime binding.");
    }
}
