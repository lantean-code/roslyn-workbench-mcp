using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.ToolExecution;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddRoslynWorkbench(this IHostApplicationBuilder builder, string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);

        var startupOptions = StartupOptionsParser.Parse(args);
        var codeActionCatalogSnapshot = new CodeActionCatalogSnapshot
        {
            Tools = BundledCodeActionCatalog.Create(),
        };
        var pluginCatalogSnapshot = new PluginCatalogLoader().Load(
            startupOptions,
            [typeof(BundledCorePlugin).Assembly],
            codeActionCatalogSnapshot.Tools.Select(static tool => tool.Metadata.Name));

        ConfigureLogging(builder.Logging);
        AddOptions(builder.Services, startupOptions);
        builder.Services.AddSingleton(codeActionCatalogSnapshot);
        AddCoreServices(builder.Services, pluginCatalogSnapshot);
        AddMcpTools(builder.Services, pluginCatalogSnapshot, codeActionCatalogSnapshot.Tools, startupOptions.ToolOutputSchemaMode);

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
                MaxTransactionRevisions = options.MaxTransactionRevisions,
                StateDirectory = options.StateDirectory,
            });
        });
    }

    private static void AddCoreServices(IServiceCollection services, PluginCatalogSnapshot pluginCatalogSnapshot)
    {
        services.AddSingleton(pluginCatalogSnapshot);
        services.AddSingleton<IMsBuildRegistrationService, MsBuildRegistrationService>();
        services.AddSingleton<IToolRequestResolver, DefaultToolRequestResolver>();
        services.AddSingleton<ICompilerDiagnosticService, DefaultCompilerDiagnosticService>();
        services.AddSingleton<IInspectionContextService, DefaultInspectionContextService>();
        services.AddSingleton<IProjectStructureService, DefaultProjectStructureService>();
        services.AddSingleton<IDependencyAnalysisService, DefaultDependencyAnalysisService>();
        services.AddSingleton<IToolExecutionServices, ToolExecutionServices>();
        services.AddSingleton<ICodeActionDiagnosticService, CodeActionDiagnosticService>();
        services.AddSingleton<ICodeActionDescriptorRegistry, CodeActionDescriptorRegistry>();
        services.AddSingleton<ICodeActionTokenService, CodeActionTokenService>();
        services.AddSingleton<ICodeActionRuntimeComposer, CodeActionRuntimeComposer>();
        services.AddSingleton<CodeActionRuntime>(static serviceProvider => serviceProvider
            .GetRequiredService<ICodeActionRuntimeComposer>()
            .Compose(serviceProvider.GetRequiredService<IOptions<CodeActionRuntimeOptions>>().Value));
        services.AddSingleton<ICodeActionRuntime>(static serviceProvider => serviceProvider.GetRequiredService<CodeActionRuntime>());
        services.AddSingleton<ICodeActionDiscoveryService, CodeActionDiscoveryService>();
        services.AddSingleton<ICodeActionResolutionService, CodeActionResolutionService>();
        services.AddSingleton<ICodeActionOperationService, CodeActionOperationService>();
        services.AddSingleton<ICodeActionQueryWorkflow, CodeActionQueryWorkflow>();
        services.AddSingleton<ICodeActionMutationWorkflow, CodeActionMutationWorkflow>();
        services.AddSingleton(static serviceProvider => new WorkspaceHostServicesAccessor(
            serviceProvider.GetRequiredService<ICodeActionRuntime>().WorkspaceHostServices));
        services.AddSingleton<IWorkspaceOperationResultFactory, WorkspaceOperationResultFactory>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<ICommitRecoveryStore, CommitRecoveryStore>();
        services.AddSingleton<IWorkspaceCommitWriter, WorkspaceCommitWriter>();
        services.AddSingleton<IWorkspaceSessionStore, WorkspaceSessionStore>();
        services.AddSingleton<IWorkspaceSelector, WorkspaceSelectorService>();
        services.AddSingleton<IWorkspaceResolverFactory, WorkspaceResolverFactory>();
        services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
        services.AddSingleton<IWorkspaceProjectInputResolver, WorkspaceProjectInputResolver>();
        services.AddSingleton<IWorkspaceChangeDetector, WorkspaceChangeDetector>();
        services.AddSingleton<IWorkspaceStateTransitions, WorkspaceStateTransitions>();
        services.AddSingleton<ISnapshotGuard, SnapshotGuard>();
        services.AddSingleton<IMutationStagingService, MutationStagingService>();
        services.AddSingleton<IWorkspaceDiffBuilder, WorkspaceDiffService>();
        services.AddSingleton<ITransactionCommitService, TransactionCommitService>();
        services.AddSingleton<IWorkspaceExecutionContextFactory, WorkspaceExecutionContextFactory>();
        services.AddSingleton<IToolExecutionContextFactory>(static serviceProvider => new PluginExecutionContextFactory(
            serviceProvider.GetRequiredService<IWorkspaceExecutionContextFactory>(),
            serviceProvider.GetRequiredService<IToolExecutionServices>()));
        services.AddSingleton<ICodeActionExecutionContextFactory>(static serviceProvider => new CodeActionExecutionContextFactory(
            serviceProvider.GetRequiredService<IWorkspaceExecutionContextFactory>(),
            serviceProvider.GetRequiredService<ICodeActionQueryWorkflow>(),
            serviceProvider.GetRequiredService<ICodeActionMutationWorkflow>()));
        services.AddSingleton<IWorkspaceLifecycleService, WorkspaceLifecycleService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IServerStatusService, ServerStatusService>();
    }

    private static void AddMcpTools(
        IServiceCollection services,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        IReadOnlyList<IRegisteredCodeActionTool> codeActionTools,
        ToolOutputSchemaMode outputSchemaMode)
    {
        var pluginVisitor = new PluginMcpToolRegistrationVisitor(services, outputSchemaMode);
        foreach (var registeredTool in pluginCatalogSnapshot.Tools)
        {
            _ = registeredTool.Accept(pluginVisitor);
        }

        var codeActionVisitor = new CodeActionMcpToolRegistrationVisitor(services, outputSchemaMode);
        foreach (var registeredTool in codeActionTools)
        {
            _ = registeredTool.Accept(codeActionVisitor);
        }

        ServerOwnedToolRegistration.AddMcpTools(services);
    }
}
