using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class WorkspaceCoordinatorFactory
{
    public static WorkspaceRuntime Create(
        IToolExecutionServices? toolExecutionServices = null)
    {
        return Create(new WorkspaceRuntimeOptions(), toolExecutionServices);
    }

    public static WorkspaceRuntime Create(
        WorkspaceRuntimeOptions options,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return CreateCore(
            MapOptions(options),
            CreateUnavailableCodeActionProviderCatalog(),
            toolExecutionServices);
    }

    public static IToolExecutionContextFactory CreateCoordinator(
        IToolExecutionServices? toolExecutionServices = null)
    {
        return Create(toolExecutionServices);
    }

    public static IToolExecutionContextFactory CreateCoordinator(
        WorkspaceRuntimeOptions options,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return Create(options, toolExecutionServices);
    }

    internal static WorkspaceRuntime CreateWithCodeActionProviderCatalog(
        ICodeActionProviderCatalog codeActionProviderCatalog,
        IToolExecutionServices? toolExecutionServices = null,
        TimeSpan? tokenLifetime = null)
    {
        return CreateWithCodeActionProviderCatalog(new WorkspaceRuntimeOptions(), codeActionProviderCatalog, toolExecutionServices, tokenLifetime);
    }

    internal static WorkspaceRuntime CreateWithCodeActionProviderCatalog(
        WorkspaceRuntimeOptions options,
        ICodeActionProviderCatalog codeActionProviderCatalog,
        IToolExecutionServices? toolExecutionServices = null,
        TimeSpan? tokenLifetime = null)
    {
        return CreateCore(MapOptions(options), codeActionProviderCatalog, toolExecutionServices, tokenLifetime);
    }

    internal static IToolExecutionContextFactory CreateCoordinatorWithCodeActionProviderCatalog(
        ICodeActionProviderCatalog codeActionProviderCatalog,
        IToolExecutionServices? toolExecutionServices = null,
        TimeSpan? tokenLifetime = null)
    {
        return CreateCoordinatorWithCodeActionProviderCatalog(new WorkspaceRuntimeOptions(), codeActionProviderCatalog, toolExecutionServices, tokenLifetime);
    }

    internal static IToolExecutionContextFactory CreateCoordinatorWithCodeActionProviderCatalog(
        WorkspaceRuntimeOptions options,
        ICodeActionProviderCatalog codeActionProviderCatalog,
        IToolExecutionServices? toolExecutionServices = null,
        TimeSpan? tokenLifetime = null)
    {
        return CreateWithCodeActionProviderCatalog(options, codeActionProviderCatalog, toolExecutionServices, tokenLifetime);
    }

    private static WorkspaceRuntime CreateCore(
        WorkspaceCoordinatorOptions options,
        ICodeActionProviderCatalog codeActionProviderCatalog,
        IToolExecutionServices? toolExecutionServices,
        TimeSpan? tokenLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(codeActionProviderCatalog);

        var optionsWrapper = Options.Create(options);
        var executionServices = toolExecutionServices ?? BundledCoreToolExecutionServicesFactory.Create();
        var sessionStore = new WorkspaceSessionStore();
        var workspaceSelector = new WorkspaceSelectorService();
        var sessionAcquirer = new WorkspaceSessionAcquirer(sessionStore, workspaceSelector);
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison();
        var workspaceLoader = new WorkspaceLoader(
            new HostConfiguredMsBuildWorkspaceFactory(codeActionProviderCatalog),
            new WorkspaceProjectCompatibilityInspector());
        var workspaceRootResolver = new WorkspaceRootResolver(fileSystem, pathComparison);
        var workspaceLoadWorkflow = new WorkspaceLoadWorkflow(workspaceLoader, workspaceRootResolver);
        var fileCommitter = new NativeAtomicFileCommitter();
        var atomicFileWriter = new AtomicFileWriter(fileSystem, fileCommitter);
        var workspaceChangeDetector = new WorkspaceChangeDetector(fileSystem, new WorkspaceProjectInputResolver());
        var workspaceStateTransitions = new WorkspaceStateTransitions();
        var resultFactory = new WorkspaceOperationResultFactory();
        var snapshotGuard = new SnapshotGuard();
        var workspaceResolverFactory = new WorkspaceResolverFactory();
        var recoveryStore = new CommitRecoveryStore(optionsWrapper, fileSystem, atomicFileWriter, pathComparison);
        var instanceStatusPublisher = new WorkspaceInstanceStatusPublisher(fileSystem, pathComparison);
        var commitWriter = new WorkspaceCommitWriter(fileSystem, atomicFileWriter, recoveryStore, fileCommitter);
        var mutationStagingService = new MutationStagingService(
            new WorkspaceOperationResultFactory(),
            sessionStore,
            new WorkspaceDiffService(),
            workspaceResolverFactory,
            instanceStatusPublisher,
            new WorkspaceMutationCandidateValidator(pathComparison));
        var codeActionDiagnosticService = new CodeActionDiagnosticService();
        var codeActionDescriptorRegistry = new CodeActionDescriptorRegistry([ControlledCodeActionDescriptorClassifier.Classify]);
        var codeActionTokenService = new CodeActionTokenService();
        var codeActionDiscoveryService = new CodeActionDiscoveryService(codeActionProviderCatalog);
        var codeActionResolutionService = new CodeActionResolutionService(
            codeActionDiscoveryService,
            codeActionDiagnosticService,
            codeActionDescriptorRegistry,
            codeActionTokenService);
        var codeActionOperationService = new CodeActionOperationService(
            codeActionDiagnosticService,
            codeActionDescriptorRegistry);
        var codeActionQueryWorkflow = new CodeActionQueryWorkflow(
            codeActionProviderCatalog,
            codeActionDiscoveryService,
            codeActionDiagnosticService,
            codeActionResolutionService,
            codeActionDescriptorRegistry,
            codeActionTokenService,
            Options.Create(new CodeActionExecutionOptions
            {
                TokenLifetime = tokenLifetime ?? TimeSpan.FromMinutes(5),
            }));
        var codeActionMutationWorkflow = new CodeActionMutationWorkflow(
            codeActionProviderCatalog,
            codeActionDiscoveryService,
            codeActionResolutionService,
            codeActionOperationService,
            codeActionDiagnosticService,
            codeActionDescriptorRegistry,
            codeActionTokenService);
        var transactionCommitService = new TransactionCommitService(
            sessionStore,
            workspaceChangeDetector,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory,
            recoveryStore,
            commitWriter,
            new WorkspaceCommitPlanner(fileSystem, pathComparison),
            new WorkspaceCommitLockManager(
                fileSystem,
                new FileStreamWorkspaceFileLockProvider()),
            instanceStatusPublisher);
        var workspaceContextFactory = new WorkspaceExecutionContextFactory(
            optionsWrapper,
            sessionStore,
            sessionAcquirer,
            workspaceChangeDetector,
            workspaceStateTransitions,
            mutationStagingService,
            workspaceResolverFactory);
        var pluginContextFactory = new PluginExecutionContextFactory(workspaceContextFactory, executionServices);
        var codeActionContextFactory = new CodeActionExecutionContextFactory(
            workspaceContextFactory,
            codeActionQueryWorkflow,
            codeActionMutationWorkflow);
        var workspaceLifecycleService = new WorkspaceLifecycleService(
            optionsWrapper,
            sessionStore,
            sessionAcquirer,
            workspaceLoader,
            workspaceRootResolver,
            workspaceLoadWorkflow,
            workspaceChangeDetector,
            workspaceStateTransitions,
            resultFactory,
            recoveryStore,
            instanceStatusPublisher);
        var transactionService = new TransactionService(
            optionsWrapper,
            sessionStore,
            sessionAcquirer,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory,
            transactionCommitService,
            new WorkspaceDiffService(),
            workspaceResolverFactory,
            instanceStatusPublisher);

        return new WorkspaceRuntime(
            pluginContextFactory,
            codeActionContextFactory,
            workspaceLifecycleService,
            transactionService);
    }

    private static ICodeActionProviderCatalog CreateUnavailableCodeActionProviderCatalog()
    {
        return new MefCodeActionProviderCatalog(Options.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
        }));
    }

    private static WorkspaceCoordinatorOptions MapOptions(WorkspaceRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = options.DefaultMaxResults,
            MaxConcurrentQueries = options.MaxConcurrentQueries,
            MaxLoadedWorkspaces = options.MaxLoadedWorkspaces,
            MaxTransactionRevisions = options.MaxTransactionRevisions,
            StateDirectory = options.StateDirectory ?? Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"),
        };
    }
}
