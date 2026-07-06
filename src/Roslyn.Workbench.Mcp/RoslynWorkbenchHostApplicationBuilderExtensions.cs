using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
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
        services.AddSingleton<ICodeActionDiagnosticService, CodeActionDiagnosticService>();
        services.AddSingleton<ICodeActionDescriptorRegistry, CodeActionDescriptorRegistry>();
        services.AddSingleton<ICodeActionTokenService, CodeActionTokenService>();
        services.AddSingleton<ICodeActionRuntimeComposer, CodeActionRuntimeComposer>();
        services.AddSingleton(static serviceProvider => serviceProvider
            .GetRequiredService<ICodeActionRuntimeComposer>()
            .Compose(serviceProvider.GetRequiredService<IOptions<CodeActionRuntimeOptions>>().Value));
        services.AddSingleton<Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionQueryWorkflow>(static serviceProvider => serviceProvider.GetRequiredService<CodeActionRuntime>().QueryWorkflow);
        services.AddSingleton<Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution.ICodeActionMutationWorkflow>(static serviceProvider => serviceProvider.GetRequiredService<CodeActionRuntime>().MutationWorkflow);
        services.AddSingleton(static serviceProvider => new WorkspaceHostServicesAccessor(serviceProvider.GetRequiredService<CodeActionRuntime>().WorkspaceHostServices));
        services.AddSingleton<IWorkspaceOperationResultFactory, WorkspaceOperationResultFactory>();
        services.AddSingleton<IWorkspaceSessionStore, WorkspaceSessionStore>();
        services.AddSingleton<IWorkspaceSelector, WorkspaceSelectorService>();
        services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
        services.AddSingleton<IWorkspaceChangeDetector, WorkspaceChangeDetector>();
        services.AddSingleton<IWorkspaceStateTransitions, WorkspaceStateTransitions>();
        services.AddSingleton<ISnapshotGuard, SnapshotGuard>();
        services.AddSingleton<IMutationStagingService, MutationStagingService>();
        services.AddSingleton<ITransactionCommitService, TransactionCommitService>();
        services.AddSingleton<IWorkspaceExecutionContextFactory, WorkspaceExecutionContextFactory>();
        services.AddSingleton<IWorkspaceLifecycleService, WorkspaceLifecycleService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IServerStatusService, ServerStatusService>();
        services.AddSingleton(static serviceProvider => (IToolExecutionContextFactory)serviceProvider.GetRequiredService<IWorkspaceExecutionContextFactory>());
        services.AddSingleton<ToolExecutor>();
    }

    private static void AddMcpTools(IServiceCollection services, PluginCatalogSnapshot pluginCatalogSnapshot)
    {
        foreach (var registeredTool in pluginCatalogSnapshot.Tools)
        {
            var pluginTool = registeredTool;
            services.AddSingleton<McpServerTool>(serviceProvider => new PluginMcpServerTool(pluginTool, serviceProvider.GetRequiredService<ToolExecutor>()));
        }

        ServerOwnedToolRegistration.AddMcpTools(services);
    }
}
