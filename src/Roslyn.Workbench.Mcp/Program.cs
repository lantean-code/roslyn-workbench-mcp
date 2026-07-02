using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var startupOptions = StartupOptionsParser.Parse(args);
        _ = MsBuildRegistration.EnsureRegistered();
        var pluginCatalogLoader = new PluginCatalogLoader();
        var pluginCatalogSnapshot = pluginCatalogLoader.Load(startupOptions, [typeof(BundledCorePlugin).Assembly]);
        var codeActionRuntime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            TokenLifetime = startupOptions.CodeActionTokenLifetime,
        });
        var workspaceCoordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            WorkspaceHostServices = codeActionRuntime.WorkspaceHostServices,
            CodeActionService = codeActionRuntime.CodeActionService,
            DefaultMaxResults = startupOptions.DefaultMaxResults,
            MaxConcurrentQueries = startupOptions.MaxConcurrentQueries,
            MaxResponseBytes = startupOptions.MaxResponseBytes,
            MaxTransactionRevisions = startupOptions.MaxTransactionRevisions,
            StateDirectory = startupOptions.StateDirectory,
        });
        var contextFactory = (IToolExecutionContextFactory)workspaceCoordinator;
        var toolExecutor = new ToolExecutor(contextFactory);
        var pluginTools = pluginCatalogSnapshot.Tools
            .Select(static registeredTool => registeredTool)
            .Select(registeredTool => new PluginMcpServerTool(registeredTool, toolExecutor))
            .Cast<ModelContextProtocol.Server.McpServerTool>()
            .ToArray();
        var lifecycleTools = WorkspaceLifecycleToolFactory.Create(workspaceCoordinator, startupOptions.ToolOutputSchemaMode);
        var transactionTools = TransactionToolFactory.Create(workspaceCoordinator, startupOptions.ToolOutputSchemaMode);
        var serverStatusTool = ServerStatusToolFactory.Create(startupOptions, pluginCatalogSnapshot, codeActionRuntime.CodeActionService.Status, pluginTools.Length + lifecycleTools.Count + transactionTools.Count + 1);
        var mcpTools = new[] { serverStatusTool }.Concat(pluginTools).Concat(lifecycleTools).Concat(transactionTools).ToArray();

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(static options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddSingleton(startupOptions);
        builder.Services.AddSingleton(pluginCatalogSnapshot);
        builder.Services.AddSingleton(workspaceCoordinator);
        builder.Services.AddSingleton(workspaceCoordinator);
        builder.Services.AddSingleton(contextFactory);
        builder.Services.AddSingleton(toolExecutor);

        var mcpBuilder = builder.Services.AddMcpServer();
        mcpBuilder.WithStdioServerTransport();
        mcpBuilder.WithTools(mcpTools);

        using var host = builder.Build();
        var server = host.Services.GetRequiredService<ModelContextProtocol.Server.McpServer>();

        await server.RunAsync();

        return 0;
    }
}
