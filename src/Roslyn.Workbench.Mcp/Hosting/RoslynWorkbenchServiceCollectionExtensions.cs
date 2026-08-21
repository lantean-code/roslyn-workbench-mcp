using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Retention;
using Roslyn.Workbench.Mcp.ToolExecution;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Sentry;

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
                options.CodeActionReferenceLifetime = startupOptions.CodeActionReferenceLifetime;
                options.WorkspaceQueryCacheSizeLimit = startupOptions.WorkspaceQueryCacheSizeLimit;
                options.PluginQueryCacheEntryLimit = startupOptions.PluginQueryCacheEntryLimit;
                options.CodeActionReferenceCacheSizeLimit = startupOptions.CodeActionReferenceCacheSizeLimit;
                options.WorkspaceQueryCacheSlidingExpiration = startupOptions.WorkspaceQueryCacheSlidingExpiration;
                options.PluginQueryCacheSlidingExpiration = startupOptions.PluginQueryCacheSlidingExpiration;
                options.MaxTransactionRevisions = startupOptions.MaxTransactionRevisions;
                options.MaxConcurrentQueries = startupOptions.MaxConcurrentQueries;
                options.ToolOutputSchemaMode = startupOptions.ToolOutputSchemaMode;
                options.StateDirectory = startupOptions.StateDirectory;
                options.ErrorReporting = startupOptions.ErrorReporting;
            })
            .ValidateOnStart();

        services.AddOptions<ErrorReportingOptions>()
            .Configure(options =>
            {
                var configured = startupOptions.ErrorReporting;
                options.ConsentMode = configured.ConsentMode;
                options.CapturedErrorCapacity = configured.CapturedErrorCapacity;
                options.CapturedErrorLifetime = configured.CapturedErrorLifetime;
                options.MaximumCapturedErrorBytes = configured.MaximumCapturedErrorBytes;
                options.PreparedSubmissionCapacity = configured.PreparedSubmissionCapacity;
                options.PreparedSubmissionLifetime = configured.PreparedSubmissionLifetime;
                options.MaximumPayloadBytes = configured.MaximumPayloadBytes;
            });

        services.AddSingleton<IValidateOptions<StartupOptions>, StartupOptionsValidator>();
        services.AddOptions<CodeActionCompositionOptions>();
        services.AddOptions<CodeActionExecutionOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                options.ReferenceLifetime = configuredStartupOptions.Value.CodeActionReferenceLifetime;
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

        services.AddOptions<WorkspaceQueryCacheOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                options.SizeLimit = configuredStartupOptions.Value.WorkspaceQueryCacheSizeLimit;
                options.SlidingExpiration = configuredStartupOptions.Value.WorkspaceQueryCacheSlidingExpiration;
            });

        services.AddOptions<PluginQueryCacheOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                options.EntryLimit = configuredStartupOptions.Value.PluginQueryCacheEntryLimit;
                options.SlidingExpiration = configuredStartupOptions.Value.PluginQueryCacheSlidingExpiration;
            });

        services.AddOptions<CodeActionReferenceCacheOptions>()
            .Configure<IOptions<StartupOptions>>((options, configuredStartupOptions) =>
            {
                options.SizeLimit = configuredStartupOptions.Value.CodeActionReferenceCacheSizeLimit;
            });
    }

    public static void AddWorkspaceServices(this IServiceCollection services)
    {
        services.AddSingleton<IMsBuildWorkspaceFactory, HostConfiguredMsBuildWorkspaceFactory>();
        services.AddSingleton<IWorkspaceOperationResultFactory, WorkspaceOperationResultFactory>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IWorkspacePathComparison, WorkspacePathComparison>();
        services.AddSingleton<IWorkspacePathNormalizer, WorkspacePathNormalizer>();
        services.AddSingleton<IWorkspacePathServiceFactory, WorkspacePathServiceFactory>();
        services.AddSingleton<IPhysicalPathContainment, PhysicalPathContainment>();
        services.AddSingleton<IAtomicFileCommitter, NativeAtomicFileCommitter>();
        services.AddSingleton<IWorkspaceInstanceStatusPublisher, WorkspaceInstanceStatusPublisher>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<IWorkspaceStateDirectorySecurity, WorkspaceStateDirectorySecurity>();
        services.AddSingleton<IWorkspaceStateDirectory, WorkspaceStateDirectory>();
        services.AddSingleton(CommitRecoveryLimits.Default);
        services.AddSingleton<ICommitRecoveryStore, CommitRecoveryStore>();
        services.AddSingleton<IWorkspaceDocumentContentService, WorkspaceDocumentContentService>();
        services.AddSingleton<IWorkspaceCommitPlanner, WorkspaceCommitPlanner>();
        services.AddSingleton<IWorkspaceFileLockProvider, FileStreamWorkspaceFileLockProvider>();
        services.AddSingleton<IWorkspaceCommitLockManager, WorkspaceCommitLockManager>();
        services.AddSingleton<IWorkspaceCommitWriter, WorkspaceCommitWriter>();
        services.AddSingleton<IWorkspaceCommitRecoveryService, WorkspaceCommitRecoveryService>();
        services.AddSingleton<IWorkspaceQueryCacheState, WorkspaceQueryCacheState>();
        services.AddSingleton<IWorkspaceQueryCacheStore, WorkspaceQueryCacheStore>();
        services.AddSingleton<IWorkspaceQueryCacheScopeFactory, WorkspaceQueryCacheScopeFactory>();
        services.AddSingleton<IPluginQueryCacheState, PluginQueryCacheState>();
        services.AddSingleton<IPluginQueryCacheStore, PluginQueryCacheStore>();
        services.AddSingleton<IWorkspaceQueryCache, WorkspaceQueryCache>();
        services.AddSingleton<IWorkspaceSnapshotLifecycleObserver, PluginQueryCacheLifecycleObserver>();
        services.AddSingleton<IWorkspaceSessionStore, WorkspaceSessionStore>();
        services.AddSingleton<IWorkspaceMsBuildPropertiesProvider, WorkspaceMsBuildPropertiesProvider>();
        services.AddSingleton<IWorkspaceSelector, WorkspaceSelectorService>();
        services.AddSingleton<IWorkspaceSelectorFactory, WorkspaceSelectorFactory>();
        services.AddSingleton<IReferenceDiscoveryService, ReferenceDiscoveryService>();
        services.AddSingleton<ITypeHierarchyService, TypeHierarchyService>();
        services.AddSingleton<IWorkspaceSessionAcquirer, WorkspaceSessionAcquirer>();
        services.AddSingleton<IWorkspaceResolverFactory, WorkspaceResolverFactory>();
        services.AddSingleton<IWorkspaceProjectCompatibilityInspector, WorkspaceProjectCompatibilityInspector>();
        services.AddSingleton<IWorkspaceMsBuildPropertiesResolver, WorkspaceMsBuildPropertiesResolver>();
        services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();
        services.AddSingleton<IWorkspaceRootResolver, WorkspaceRootResolver>();
        services.AddSingleton<IWorkspaceLoadWorkflow, WorkspaceLoadWorkflow>();
        services.AddSingleton<IWorkspaceProjectInputResolver, WorkspaceProjectInputResolver>();
        services.AddSingleton<IWorkspaceExternalInputChangeMonitorFactory, WorkspaceExternalInputChangeMonitorFactory>();
        services.AddSingleton<IWorkspaceInputChangeMonitorFactory, WorkspaceInputChangeMonitorFactory>();
        services.AddSingleton<IWorkspaceChangeDetector, WorkspaceChangeDetector>();
        services.AddSingleton<IWorkspaceReadOnlyDocumentValidator, WorkspaceReadOnlyDocumentValidator>();
        services.AddSingleton<IWorkspaceStateTransitions, WorkspaceStateTransitions>();
        services.AddSingleton<ISnapshotGuard, SnapshotGuard>();
        services.AddSingleton<IWorkspaceMutationCandidateValidator, WorkspaceMutationCandidateValidator>();
        services.AddSingleton<IAddedDocumentProjectContextPropagator, AddedDocumentProjectContextPropagator>();
        services.AddSingleton<IRemovedDocumentProjectContextPropagator, RemovedDocumentProjectContextPropagator>();
        services.AddSingleton<ILinkedDocumentChangeMerger, LinkedDocumentChangeMerger>();
        services.AddSingleton<IWorkspaceMutationCandidateProcessor, WorkspaceMutationCandidateProcessor>();
        services.AddSingleton<IWorkspaceMutationCandidateIdentityService, WorkspaceMutationCandidateIdentityService>();
        services.AddSingleton<IMutationStagingService, MutationStagingService>();
        services.AddSingleton<IWorkspaceDiffBuilder, WorkspaceDiffService>();
        services.AddSingleton<ITransactionCommitService, TransactionCommitService>();
        services.AddSingleton<IProjectStructureService, ProjectStructureService>();
        services.AddSingleton<IProjectTargetFrameworkResolver, ProjectTargetFrameworkResolver>();
        services.AddSingleton<IWorkspaceExecutionContextFactory, WorkspaceExecutionContextFactory>();
        services.AddSingleton<IWorkspaceSessionCleanup, WorkspaceSessionCleanup>();
        services.AddSingleton<IWorkspaceLifecycleService, WorkspaceLifecycleService>();
        services.AddSingleton<ITransactionService, TransactionService>();
    }

    public static void AddPluginServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolRequestResolver, ToolRequestResolver>();
        services.AddSingleton<ICompilerDiagnosticService, CompilerDiagnosticService>();
        services.AddSingleton<IInspectionContextService, InspectionContextService>();
        services.AddSingleton<IDependencyAnalysisService, DependencyAnalysisService>();
        services.AddSingleton<IToolExecutionServices, ToolExecutionServices>();
        services.AddSingleton<IQueryResultCacheScopeFactory, QueryResultCacheScopeFactory>();
        services.AddSingleton<IToolExecutionContextFactory, PluginExecutionContextFactory>();
    }

    public static void AddCodeActionServices(this IServiceCollection services)
    {
        services.AddSingleton<ICodeActionAnalyzerActivator, CodeActionAnalyzerActivator>();
        services.AddSingleton<ICodeActionBuiltInAnalyzerIndex, CodeActionBuiltInAnalyzerIndex>();
        services.AddSingleton<ICodeActionDiagnosticService, CodeActionDiagnosticService>();
        services.AddSingleton<ICodeActionReferenceState, CodeActionReferenceState>();
        services.AddSingleton<ICodeActionReferenceStore, CodeActionReferenceStore>();
        services.AddSingleton<IWorkspaceSnapshotLifecycleObserver, CodeActionReferenceLifecycleObserver>();
        services.AddSingleton<ICodeActionInfoFactory, CodeActionInfoFactory>();
        services.AddSingleton<IMefHostExportProviderCompatibilityAdapter, MefHostExportProviderCompatibilityAdapter>();
        services.AddSingleton<ICodeActionPolicy, CodeActionPolicy>();
        services.AddSingleton<ICodeActionComposition, MefCodeActionComposition>();
        services.AddSingleton<ICodeActionProviderSelection, CodeActionProviderSelection>();
        services.AddSingleton<ICodeActionDiscoveryService, CodeActionDiscoveryService>();
        services.AddSingleton<ICodeActionResolver, CodeActionResolver>();
        services.AddSingleton<IPreparedFixAllResolver, PreparedFixAllResolver>();
        services.AddSingleton<ICodeActionEvaluator, CodeActionEvaluator>();
        services.AddSingleton<IFixAllActionFactory, FixAllActionFactory>();
        services.AddSingleton<ICodeActionSolutionChangeCounter, CodeActionSolutionChangeCounter>();
        services.AddSingleton<ICodeActionStager, CodeActionStager>();
        services.AddSingleton<ICodeActionScopeResolver, CodeActionScopeResolver>();
        services.AddSingleton<ICodeActionToolRequestResolver, CodeActionToolRequestResolver>();
        services.AddSingleton<ICodeActionExecutionContextFactory, CodeActionExecutionContextFactory>();
    }

    public static void AddHostServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddBoundedExpiringStore<Guid, CapturedErrorRecord, CapturedErrorRetentionPolicy>();
        services.AddSingleton<ICapturedErrorStore, CapturedErrorStore>();
        services.AddSingleton<IErrorCaptureService, ErrorCaptureService>();
        services.AddSingleton<IExternalErrorReportProjector, ExternalErrorReportProjector>();
        services.AddBoundedExpiringStore<string, PreparedSubmission, PreparedSubmissionRetentionPolicy>();
        services.AddSingleton<IPreparedSubmissionStore, PreparedSubmissionStore>();
        services.AddSingleton<IErrorReportingConsentStore, ErrorReportingConsentStore>();
        services.AddSingleton<IErrorReportingConsentService, ErrorReportingConsentService>();
        services.AddSingleton<IWorkspaceSnapshotLifecycleObserver, ErrorReportingConsentLifecycleObserver>();
        services.AddSingleton<IErrorReportingAvailabilityService, ErrorReportingAvailabilityService>();
        AddErrorReportDispatcher(services, SentrySdkPolicy.EmbeddedConfiguration);
        services.AddSingleton<IMcpSdkSchemaProvider, McpSdkSchemaProvider>();
        services.AddSingleton<IToolSchemaFactory, ToolSchemaFactory>();
        services.AddSingleton<IMcpToolProtocolFactory, McpToolProtocolFactory>();
        services.AddSingleton<IRequestObjectGraphValidator, RequestObjectGraphValidator>();
        services.AddSingleton<IToolRequestBinder, ToolRequestBinder>();
        services.AddSingleton<UnhandledToolExceptionFilter>();
        services.AddSingleton<IMsBuildRegistrationService, MsBuildRegistrationService>();
        services.AddSingleton<IServerStatusService, ServerStatusService>();
        AddPluginCatalogServices(services);
    }

    public static void AddBoundedExpiringStore<TKey, TValue, TPolicy>(
        this IServiceCollection services)
        where TKey : notnull
        where TValue : class
        where TPolicy : class, IBoundedExpiringStorePolicy<TKey, TValue>
    {
        services.AddSingleton<IBoundedExpiringStorePolicy<TKey, TValue>, TPolicy>();
        services.AddSingleton<IBoundedExpiringStore<TKey, TValue>, BoundedExpiringStore<TKey, TValue>>();
    }

    public static void AddMcpTools(
        this IServiceCollection services,
        IReadOnlyList<IRegisteredCodeActionTool> codeActionTools)
    {
        services.AddMcpTools(
            codeActionTools,
            new ErrorReportingOptions());
    }

    public static void AddMcpTools(
        this IServiceCollection services,
        IReadOnlyList<IRegisteredCodeActionTool> codeActionTools,
        ErrorReportingOptions errorReportingOptions)
    {
        var codeActionVisitor = new CodeActionMcpToolRegistrationVisitor(services);
        foreach (var registeredTool in codeActionTools)
        {
            registeredTool.Accept(codeActionVisitor);
        }

        ServerOwnedToolRegistration.AddMcpTools(services, errorReportingOptions);
    }

    public static void AddStartupPrerequisites(this IServiceCollection services)
    {
        services.AddHostedService<StartupConfigurationReporter>();
        services.AddHostedService<StartupPrerequisiteLifecycleService>();
        services.AddHostedService<PluginCatalogStartupLifecycleService>();
        services.AddHostedService<WorkspaceShutdownLifecycleService>();
    }

    public static void ConfigureRoslynWorkbenchLogging(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddConsole(static options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    }

    public static void AddErrorReportDispatcher(
        IServiceCollection services,
        SentryProviderConfiguration? sentryConfiguration)
    {
        if (sentryConfiguration is null)
        {
            services.AddSingleton<IErrorReportDispatcher, LoggingErrorReportDispatcher>();
            return;
        }

        services.AddSingleton(sentryConfiguration);
        services.AddSingleton<SentryOptions, RoslynWorkbenchSentryOptions>();
        services.AddSingleton<ISentryClient, SentryClient>();
        services.AddSingleton<IErrorReportDispatcher, SentryErrorReportDispatcher>();
    }

    private static void AddPluginCatalogServices(IServiceCollection services)
    {
        services.AddSingleton<IPluginHandlerTypeInspector, PluginHandlerTypeInspector>();
        services.AddSingleton<IPluginHandlerContractResolver, PluginHandlerContractResolver>();
        services.AddSingleton<IPluginHandlerWarningInspector, PluginHandlerWarningInspector>();
        services.AddSingleton<IPluginConfigurationPreparer, PluginConfigurationPreparer>();
        services.AddSingleton<IPluginToolRegistrationMaterializer, PluginToolRegistrationMaterializer>();
        services.AddSingleton<IPluginComposer, MefPluginComposer>();
        services.AddSingleton<ILoadedPluginPreparer, LoadedPluginPreparer>();
        services.AddSingleton<IPluginEntryPointValidator, PluginEntryPointValidator>();
        services.AddSingleton<IPluginPackagePathPolicy, PluginPackagePathPolicy>();
        services.AddSingleton<IPluginAssemblyMetadataReader, PluginAssemblyMetadataReader>();
        services.AddSingleton<IPluginLoadContextFactory, PluginLoadContextFactory>();
        services.AddSingleton<IPluginCandidatePreparer, PluginCandidatePreparer>();
        services.AddSingleton<IPluginTransportSchemaPreflight, PluginTransportSchemaPreflight>();
        services.AddSingleton<IPluginCatalogEntryMaterializer, PluginCatalogEntryMaterializer>();
        services.AddSingleton<IPluginPackageDiscovery, PluginPackageDiscovery>();
        services.AddSingleton<IPluginCollisionPolicy, PluginCollisionPolicy>();
        services.AddSingleton<IPluginCatalogLoader, PluginCatalogLoader>();
        services.AddSingleton<IPluginMcpServerToolFactory, PluginMcpServerToolFactory>();
        services.AddSingleton<IPluginCatalogState, PluginCatalogState>();
        services.AddSingleton<IPluginMcpRequestHandler, PluginMcpRequestHandler>();
    }
}
