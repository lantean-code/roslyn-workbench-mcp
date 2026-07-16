using System.Text.Json;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class CodeActionToolTestHarness
{
    public static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        IWorkspaceRuntime workspaceRuntime,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(workspaceRuntime);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        if (workspaceRuntime is not WorkspaceRuntime runtime)
        {
            throw new ArgumentException("The Code Action test harness requires a WorkspaceRuntime instance.", nameof(workspaceRuntime));
        }

        var registration = BundledCodeActionCatalog.Create()
            .Single(tool => string.Equals(tool.Metadata.Name, toolName, StringComparison.Ordinal));
        var serverTool = registration.Accept(new CodeActionMcpServerToolFactory(
            runtime.CodeActionHandlerServices,
            runtime.CodeActionContextFactory));
        var result = await serverTool.InvokeArgumentsAsync(arguments, CancellationToken.None);

        if (result.IsError != !expectProtocolSuccess)
        {
            throw new InvalidOperationException(
                $"Expected protocol success to be '{expectProtocolSuccess}', but 'IsError' was '{result.IsError}'.");
        }

        return PluginToolTestHarness.DeserializeToolResult<TResponse>(result.StructuredContent!.Value, toolName);
    }
}
