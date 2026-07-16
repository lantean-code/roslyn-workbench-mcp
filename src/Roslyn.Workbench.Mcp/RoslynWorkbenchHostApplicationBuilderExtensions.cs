using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

internal static class RoslynWorkbenchHostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddRoslynWorkbench(this IHostApplicationBuilder builder, string[] args)
    {
        var startupConfiguration = StartupOptionsResolver.Resolve(args);
        var startupOptions = startupConfiguration.Options;
        new StartupOptionsValidator().EnsureValid(startupOptions);
        var codeActionCatalogSnapshot = new CodeActionCatalogSnapshot
        {
            Tools = BundledCodeActionCatalog.Create(),
        };
        var pluginCatalogSnapshot = PluginCatalogComposition.CreateLoader().Load(
            startupOptions,
            [typeof(BundledCorePlugin).Assembly],
            codeActionCatalogSnapshot.Tools
                .Select(static tool => tool.Metadata.Name)
                .Concat(ServerOwnedToolRegistration.ToolNames));

        builder.Logging.ConfigureLogging();
        builder.Services.AddOptions(startupOptions);
        builder.Services.AddSingleton(startupConfiguration);
        builder.Services.AddSingleton(codeActionCatalogSnapshot);
        builder.Services.AddCoreServices(pluginCatalogSnapshot);
        builder.Services.AddMcpTools(pluginCatalogSnapshot, codeActionCatalogSnapshot.Tools);

        builder.Services.AddHostedService<StartupConfigurationReporter>();
        builder.Services.AddHostedService<MsBuildRegistrationHostedService>();
        builder.Services.AddHostedService<WorkspaceCommitRecoveryHostedService>();
        builder.Services.AddMcpServer().WithStdioServerTransport();

        return builder;
    }

    private static void ConfigureLogging(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole(static options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }

    private static void AddOptions(this IServiceCollection services, StartupOptions startupOptions)
    {
        services.AddOptions<StartupOptions>()
            .Configure(options =>
            {
                options.PluginDirectories = startupOptions.PluginDirectories;
                options.DefaultMaxResults = startupOptions.DefaultMaxResults;
                options.CodeActionTokenLifetime = startupOptions.CodeActionTokenLifetime;
                options.MaxTransactionRevisions = startupOptions.MaxTransactionRevisions;
                options.MaxConcurrentQueries = startupOptions.MaxConcurrentQueries;
                options.ToolOutputSchemaMode = startupOptions.ToolOutputSchemaMode;
                options.StateDirectory = startupOptions.StateDirectory;
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<StartupOptions>, StartupOptionsValidator>();
        services.AddOptions<CodeActionCompositionOptions>();
        services.AddOptions<CodeActionExecutionOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                options.TokenLifetime = configuredStartupOptions.Value.CodeActionTokenLifetime;
            });
        services.AddOptions<WorkspaceCoordinatorOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                var configured = configuredStartupOptions.Value;
                options.DefaultMaxResults = configured.DefaultMaxResults;
                options.MaxConcurrentQueries = configured.MaxConcurrentQueries;
                options.MaxTransactionRevisions = configured.MaxTransactionRevisions;
                options.StateDirectory = configured.StateDirectory;
            });
    }

    private static void AddCoreServices(this IServiceCollection services, PluginCatalogSnapshot pluginCatalogSnapshot)
    {
        services.AddSingleton(pluginCatalogSnapshot);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMcpSdkSchemaProvider, McpSdkSchemaProvider>();
        services.AddSingleton<ToolSchemaFactory>();
        services.AddSingleton<IMcpToolProtocolFactory, McpToolProtocolFactory>();
        services.AddSingleton<IMsBuildRegistrationService, MsBuildRegistrationService>();
        services.AddSingleton<IToolRequestResolver, DefaultToolRequestResolver>();
        services.AddSingleton<ICompilerDiagnosticService, DefaultCompilerDiagnosticService>();
        services.AddSingleton<IInspectionContextService, DefaultInspectionContextService>();
        services.AddSingleton<IProjectStructureService, DefaultProjectStructureService>();
        services.AddSingleton<IDependencyAnalysisService, DefaultDependencyAnalysisService>();
        services.AddSingleton<IToolExecutionServices, ToolExecutionServices>();
        services.AddSingleton<ICodeActionAnalyzerActivator, CodeActionAnalyzerActivator>();
        services.AddSingleton<ICodeActionDiagnosticService, CodeActionDiagnosticService>();
        services.AddSingleton<ICodeActionDescriptorRegistry, CodeActionDescriptorRegistry>();
        services.AddSingleton<ICodeActionTokenService, CodeActionTokenService>();
        services.AddSingleton<ICodeActionInfoFactory, CodeActionInfoFactory>();
        services.AddSingleton<IMefHostExportProviderCompatibilityAdapter, MefHostExportProviderCompatibilityAdapter>();
        services.AddSingleton<ICodeActionProviderCatalog, MefCodeActionProviderCatalog>();
        services.AddSingleton<ICodeActionDiscoveryService, CodeActionDiscoveryService>();
        services.AddSingleton<ICodeActionResolutionService, CodeActionResolutionService>();
        services.AddSingleton<ICodeActionOperationService, CodeActionOperationService>();
        services.AddSingleton<ICodeActionSolutionChangeCounter, CodeActionSolutionChangeCounter>();
        services.AddSingleton<ICodeActionReplayService, CodeActionReplayService>();
        services.AddSingleton<ICodeActionScopeResolver, CodeActionScopeResolver>();
        services.AddSingleton<ICodeActionFixAllService, CodeActionFixAllService>();
        services.AddSingleton<ICodeActionScopedFixService, CodeActionScopedFixService>();
        services.AddSingleton<ICodeActionLocationFixService, CodeActionLocationFixService>();
        services.AddSingleton<IMsBuildWorkspaceFactory, HostConfiguredMsBuildWorkspaceFactory>();
        services.AddSingleton<IWorkspaceOperationResultFactory, WorkspaceOperationResultFactory>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IWorkspacePathComparison, WorkspacePathComparison>();
        services.AddSingleton<IAtomicFileCommitter, NativeAtomicFileCommitter>();
        services.AddSingleton<IWorkspaceInstanceStatusPublisher, WorkspaceInstanceStatusPublisher>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<ICommitRecoveryStore, CommitRecoveryStore>();
        services.AddSingleton<IWorkspaceCommitPlanner, WorkspaceCommitPlanner>();
        services.AddSingleton<IWorkspaceFileLockProvider, FileStreamWorkspaceFileLockProvider>();
        services.AddSingleton<IWorkspaceCommitLockManager, WorkspaceCommitLockManager>();
        services.AddSingleton<IWorkspaceCommitWriter, WorkspaceCommitWriter>();
        services.AddSingleton<IWorkspaceCommitRecoveryService, WorkspaceCommitRecoveryService>();
        services.AddSingleton<IWorkspaceSessionStore, WorkspaceSessionStore>();
        services.AddSingleton<IWorkspaceSelector, WorkspaceSelectorService>();
        services.AddSingleton<IWorkspaceSessionAcquirer, WorkspaceSessionAcquirer>();
        services.AddSingleton<IWorkspaceResolverFactory, WorkspaceResolverFactory>();
        services.AddSingleton<IWorkspaceProjectCompatibilityInspector, WorkspaceProjectCompatibilityInspector>();
        services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
        services.AddSingleton<IWorkspaceRootResolver, WorkspaceRootResolver>();
        services.AddSingleton<IWorkspaceLoadWorkflow, WorkspaceLoadWorkflow>();
        services.AddSingleton<IWorkspaceProjectInputResolver, WorkspaceProjectInputResolver>();
        services.AddSingleton<IWorkspaceChangeDetector, WorkspaceChangeDetector>();
        services.AddSingleton<IWorkspaceStateTransitions, WorkspaceStateTransitions>();
        services.AddSingleton<ISnapshotGuard, SnapshotGuard>();
        services.AddSingleton<IWorkspaceMutationCandidateValidator, WorkspaceMutationCandidateValidator>();
        services.AddSingleton<IMutationStagingService, MutationStagingService>();
        services.AddSingleton<IWorkspaceDiffBuilder, WorkspaceDiffService>();
        services.AddSingleton<ITransactionCommitService, TransactionCommitService>();
        services.AddSingleton<IWorkspaceExecutionContextFactory, WorkspaceExecutionContextFactory>();
        services.AddSingleton<IToolExecutionContextFactory, PluginExecutionContextFactory>();
        services.AddSingleton<ICodeActionExecutionContextFactory, CodeActionExecutionContextFactory>();
        services.AddSingleton<IWorkspaceLifecycleService, WorkspaceLifecycleService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<IServerStatusService, ServerStatusService>();
    }

    private static void AddMcpTools(
        this IServiceCollection services,
        PluginCatalogSnapshot pluginCatalogSnapshot,
        IReadOnlyList<IRegisteredCodeActionTool> codeActionTools)
    {
        var pluginVisitor = new PluginMcpToolRegistrationVisitor(services);
        foreach (var registeredTool in pluginCatalogSnapshot.Tools)
        {
            _ = registeredTool.Accept(pluginVisitor);
        }

        var codeActionVisitor = new CodeActionMcpToolRegistrationVisitor(services);
        foreach (var registeredTool in codeActionTools)
        {
            _ = registeredTool.Accept(codeActionVisitor);
        }

        ServerOwnedToolRegistration.AddMcpTools(services);
    }
}
