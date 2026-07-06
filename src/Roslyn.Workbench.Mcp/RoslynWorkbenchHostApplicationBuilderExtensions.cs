using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
    private const int _serverOwnedToolCount = 11;

    public static IHostApplicationBuilder AddRoslynWorkbench(this IHostApplicationBuilder builder, string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);

        var startupOptions = StartupOptionsParser.Parse(args);
        var pluginCatalogSnapshot = new PluginCatalogLoader().Load(startupOptions, [typeof(BundledCorePlugin).Assembly]);

        ConfigureLogging(builder.Logging);
        AddOptions(builder.Services, startupOptions);
        AddCoreServices(builder.Services, pluginCatalogSnapshot);
        AddMcpTools(builder.Services, pluginCatalogSnapshot);

        builder.Services.AddHostedService<MsBuildRegistrationHostedService>();
        builder.Services.AddMcpServer().WithStdioServerTransport();

        return builder;
    }

    private static void ConfigureLogging(ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole(static options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }

    private static void AddOptions(IServiceCollection services, StartupOptions startupOptions)
    {
        services.AddSingleton<IOptions<StartupOptions>>(Options.Create(startupOptions));
        services.AddSingleton<IOptions<CodeActionRuntimeOptions>>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
            return Options.Create(new CodeActionRuntimeOptions
            {
                TokenLifetime = options.CodeActionTokenLifetime,
            });
        });
        services.AddSingleton<IOptions<WorkspaceCoordinatorOptions>>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
            return Options.Create(new WorkspaceCoordinatorOptions
            {
                DefaultMaxResults = options.DefaultMaxResults,
                MaxConcurrentQueries = options.MaxConcurrentQueries,
                MaxResponseBytes = options.MaxResponseBytes,
                MaxTransactionRevisions = options.MaxTransactionRevisions,
                StateDirectory = options.StateDirectory,
            });
        });
    }

    private static void AddCoreServices(IServiceCollection services, PluginCatalogSnapshot pluginCatalogSnapshot)
    {
        services.AddSingleton(pluginCatalogSnapshot);
        services.AddSingleton<IMsBuildRegistrationService, MsBuildRegistrationService>();
        services.AddSingleton<IToolResultShaper, DefaultToolResultShaper>();
        services.AddSingleton<IToolRequestResolver, DefaultToolRequestResolver>();
        services.AddSingleton<IReplayCodeActionExecutor, ReplayCodeActionExecutor>();
        services.AddSingleton<ICompilerDiagnosticService, DefaultCompilerDiagnosticService>();
        services.AddSingleton<IInspectionContextService, DefaultInspectionContextService>();
        services.AddSingleton<IProjectStructureService, DefaultProjectStructureService>();
        services.AddSingleton<IDependencyAnalysisService, DefaultDependencyAnalysisService>();
        services.AddSingleton<IToolExecutionServices, ToolExecutionServices>();
        services.AddSingleton(static serviceProvider => CodeActionRuntimeFactory.Create(serviceProvider.GetRequiredService<IOptions<CodeActionRuntimeOptions>>().Value));
        services.AddSingleton<IWorkspaceCoordinator, WorkspaceCoordinator>();
        services.AddSingleton(static serviceProvider => (IToolExecutionContextFactory)serviceProvider.GetRequiredService<IWorkspaceCoordinator>());
        services.AddSingleton<ToolExecutor>();
    }

    private static void AddMcpTools(IServiceCollection services, PluginCatalogSnapshot pluginCatalogSnapshot)
    {
        services.AddSingleton<McpServerTool>(static serviceProvider =>
        {
            var startupOptions = serviceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
            var snapshot = serviceProvider.GetRequiredService<PluginCatalogSnapshot>();
            var msBuildRegistrationService = serviceProvider.GetRequiredService<IMsBuildRegistrationService>();
            var codeActionStatus = serviceProvider.GetRequiredService<CodeActionRuntime>().CodeActionService.Status;
            return ServerStatusToolFactory.Create(
                startupOptions,
                snapshot,
                msBuildRegistrationService,
                codeActionStatus,
                snapshot.Tools.Count + _serverOwnedToolCount);
        });

        foreach (var registeredTool in pluginCatalogSnapshot.Tools)
        {
            var pluginTool = registeredTool;
            services.AddSingleton<McpServerTool>(serviceProvider => new PluginMcpServerTool(pluginTool, serviceProvider.GetRequiredService<ToolExecutor>()));
        }

        services.AddSingleton<McpServerTool>(static serviceProvider => CreateWorkspaceLifecycleTool(serviceProvider, "workspace-open"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateWorkspaceLifecycleTool(serviceProvider, "workspace-list"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateWorkspaceLifecycleTool(serviceProvider, "workspace-close"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateWorkspaceLifecycleTool(serviceProvider, "workspace-status"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateWorkspaceLifecycleTool(serviceProvider, "workspace-reload"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateTransactionTool(serviceProvider, "transaction-start"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateTransactionTool(serviceProvider, "transaction-preview"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateTransactionTool(serviceProvider, "transaction-history"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateTransactionTool(serviceProvider, "transaction-commit"));
        services.AddSingleton<McpServerTool>(static serviceProvider => CreateTransactionTool(serviceProvider, "transaction-rollback"));
    }

    private static McpServerTool CreateWorkspaceLifecycleTool(IServiceProvider serviceProvider, string toolName)
    {
        var startupOptions = serviceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
        var coordinator = serviceProvider.GetRequiredService<IWorkspaceCoordinator>();

        return WorkspaceLifecycleToolFactory.Create(coordinator, startupOptions.ToolOutputSchemaMode)
            .Single(tool => string.Equals(tool.ProtocolTool.Name, toolName, StringComparison.Ordinal));
    }

    private static McpServerTool CreateTransactionTool(IServiceProvider serviceProvider, string toolName)
    {
        var startupOptions = serviceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
        var coordinator = serviceProvider.GetRequiredService<IWorkspaceCoordinator>();

        return TransactionToolFactory.Create(coordinator, startupOptions.ToolOutputSchemaMode)
            .Single(tool => string.Equals(tool.ProtocolTool.Name, toolName, StringComparison.Ordinal));
    }
}
