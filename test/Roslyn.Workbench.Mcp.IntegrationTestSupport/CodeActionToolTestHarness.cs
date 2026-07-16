using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Protocol;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

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
        var services = new ServiceCollection();
        foreach (var descriptor in runtime.CodeActionHandlerServices)
        {
            ((ICollection<ServiceDescriptor>)services).Add(descriptor);
        }

        _ = registration.Accept(new CodeActionMcpToolRegistrationVisitor(services));
        services.AddSingleton(runtime.CodeActionContextFactory);
        services.AddSingleton<IMcpToolProtocolFactory>(new McpToolProtocolFactory(
            new ToolSchemaFactory(new McpSdkSchemaProvider())));
        services.AddSingleton<IOptions<StartupOptions>>(Options.Create(new StartupOptions()));
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var serverTool = (McpServerToolBase)serviceProvider.GetRequiredService<McpServerTool>();
        var result = await serverTool.InvokeArgumentsAsync(arguments, CancellationToken.None);

        if (result.IsError != !expectProtocolSuccess)
        {
            throw new InvalidOperationException(
                $"Expected protocol success to be '{expectProtocolSuccess}', but 'IsError' was '{result.IsError}'.");
        }

        return PluginToolTestHarness.DeserializeToolResult<TResponse>(result.StructuredContent!.Value, toolName);
    }
}
