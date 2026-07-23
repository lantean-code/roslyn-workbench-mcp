using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Plugins.Core.Analysis;
using Roslyn.Workbench.Mcp.Plugins.Core.Context;
using Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;
using Roslyn.Workbench.Mcp.Plugins.Core.Execution;
using Roslyn.Workbench.Mcp.Plugins.Core.Projects;
using Roslyn.Workbench.Mcp.Plugins.Core.Resolution;
using Roslyn.Workbench.Mcp.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Caching;

namespace Roslyn.Workbench.Mcp.Hosting;

internal static class RoslynWorkbenchServiceCollectionExtensions
{
    public static void AddRoslynWorkbenchOptions(this IServiceCollection services, StartupOptions startupOptions)
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
        services.AddOptions<WorkspaceOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                var configured = configuredStartupOptions.Value;
                options.DefaultMaxResults = configured.DefaultMaxResults;
                options.MaxConcurrentQueries = configured.MaxConcurrentQueries;
                options.MaxTransactionRevisions = configured.MaxTransactionRevisions;
                options.StateDirectory = configured.StateDirectory;
            });
    }

    public static void AddWorkspaceServices(this IServiceCollection services)
    {
        services.AddOptions<QueryCacheOptions>();
        services.AddSingleton<IConfigureOptions<MemoryCacheOptions>, QueryCacheMemoryOptionsConfiguration>();
        services.AddMemoryCache();
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
        services.AddSingleton<WorkspaceQueryCacheState>();
        services.AddSingleton<IQueryCacheInvalidationTokenSource>(static provider => provider.GetRequiredService<WorkspaceQueryCacheState>());
        services.AddSingleton<IWorkspaceQueryCacheState>(static provider => provider.GetRequiredService<WorkspaceQueryCacheState>());
        services.AddSingleton<IQueryCache, QueryCache>();
        services.AddSingleton<IWorkspaceQueryCache, WorkspaceQueryCache>();
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
        services.AddSingleton<ILinkedDocumentChangeMerger, LinkedDocumentChangeMerger>();
        services.AddSingleton<IMutationStagingService, MutationStagingService>();
        services.AddSingleton<IWorkspaceDiffBuilder, WorkspaceDiffService>();
        services.AddSingleton<ITransactionCommitService, TransactionCommitService>();
        services.AddSingleton<IWorkspaceExecutionContextFactory, WorkspaceExecutionContextFactory>();
        services.AddSingleton<IWorkspaceLifecycleService, WorkspaceLifecycleService>();
        services.AddSingleton<ITransactionService, TransactionService>();
    }

    public static void AddPluginServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolRequestResolver, ToolRequestResolver>();
        services.AddSingleton<ICompilerDiagnosticService, CompilerDiagnosticService>();
        services.AddSingleton<IInspectionContextService, InspectionContextService>();
        services.AddSingleton<IProjectStructureService, ProjectStructureService>();
        services.AddSingleton<IProjectTargetFrameworkResolver, ProjectTargetFrameworkResolver>();
        services.AddSingleton<IDependencyAnalysisService, DependencyAnalysisService>();
        services.AddSingleton<IToolExecutionServices, ToolExecutionServices>();
        services.AddSingleton<IToolExecutionContextFactory, PluginExecutionContextFactory>();
    }

    public static void AddCodeActionServices(this IServiceCollection services)
    {
        services.AddSingleton<ICodeActionAnalyzerActivator, CodeActionAnalyzerActivator>();
        services.AddSingleton<ICodeActionDiagnosticService, CodeActionDiagnosticService>();
        services.AddSingleton<ICodeActionDescriptorRegistry, CodeActionDescriptorRegistry>();
        services.AddSingleton<ICodeActionTokenService, CodeActionTokenService>();
        services.AddSingleton<ICodeActionInfoFactory, CodeActionInfoFactory>();
        services.AddSingleton<IMefHostExportProviderCompatibilityAdapter, MefHostExportProviderCompatibilityAdapter>();
        services.AddSingleton<ICodeActionProviderCatalog, MefCodeActionProviderCatalog>();
        services.AddSingleton<ICodeActionDiscoveryService, CodeActionDiscoveryService>();
        services.AddSingleton<ICodeActionResolver, CodeActionResolver>();
        services.AddSingleton<ICodeActionEvaluator, CodeActionEvaluator>();
        services.AddSingleton<IFixAllActionFactory, FixAllActionFactory>();
        services.AddSingleton<ICodeActionSolutionChangeCounter, CodeActionSolutionChangeCounter>();
        services.AddSingleton<ICodeActionTokenStager, CodeActionTokenStager>();
        services.AddSingleton<ICodeActionSelectionStager, CodeActionSelectionStager>();
        services.AddSingleton<ICodeActionScopeResolver, CodeActionScopeResolver>();
        services.AddSingleton<ICodeActionToolRequestResolver, CodeActionToolRequestResolver>();
        services.AddSingleton<IScopedCodeFixCandidateResolver, ScopedCodeFixCandidateResolver>();
        services.AddSingleton<ICodeActionFixAllStager, CodeActionFixAllStager>();
        services.AddSingleton<IScopedCodeFixStager, ScopedCodeFixStager>();
        services.AddSingleton<ILocationCodeFixStager, LocationCodeFixStager>();
        services.AddSingleton<ICodeActionExecutionContextFactory, CodeActionExecutionContextFactory>();
    }

    public static void AddHostServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IMcpSdkSchemaProvider, McpSdkSchemaProvider>();
        services.AddSingleton<ToolSchemaFactory>();
        services.AddSingleton<IMcpToolProtocolFactory, McpToolProtocolFactory>();
        services.AddSingleton<UnhandledToolExceptionFilter>();
        services.AddSingleton<IMsBuildRegistrationService, MsBuildRegistrationService>();
        services.AddSingleton<IServerStatusService, ServerStatusService>();
    }

    public static void AddMcpTools(
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

    public static void AddStartupPrerequisites(this IServiceCollection services)
    {
        services.AddHostedService<StartupConfigurationReporter>();
        services.AddHostedService<StartupPrerequisiteLifecycleService>();
    }

    public static void ConfigureRoslynWorkbenchLogging(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole(static options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }
}
