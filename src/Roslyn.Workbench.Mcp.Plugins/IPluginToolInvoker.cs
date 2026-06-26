namespace Roslyn.Workbench.Mcp.Plugins;

internal interface IPluginToolInvoker
{
    ValueTask<PluginExecutionResultBox> ExecuteAsync(object request, IToolExecutionContext context, CancellationToken cancellationToken);
}
