namespace Roslyn.Workbench.Mcp.Plugins;

internal sealed class NullPluginToolInvoker : IPluginToolInvoker
{
    public static readonly NullPluginToolInvoker Instance = new();

    private NullPluginToolInvoker()
    {
    }

    public ValueTask<PluginExecutionResultBox> ExecuteAsync(object request, IToolExecutionContext context, CancellationToken cancellationToken)
    {
        _ = request;
        _ = context;
        _ = cancellationToken;

        throw new InvalidOperationException("RegisteredTool does not have an invocation delegate.");
    }
}
