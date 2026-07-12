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
            CreateUnavailableCodeActionRuntime(),
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

    internal static WorkspaceRuntime CreateWithCodeActionRuntime(
        CodeActionRuntime codeActionRuntime,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return CreateWithCodeActionRuntime(new WorkspaceRuntimeOptions(), codeActionRuntime, toolExecutionServices);
    }

    internal static WorkspaceRuntime CreateWithCodeActionRuntime(
        WorkspaceRuntimeOptions options,
        CodeActionRuntime codeActionRuntime,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return CreateCore(MapOptions(options), codeActionRuntime, toolExecutionServices);
    }

    internal static IToolExecutionContextFactory CreateCoordinatorWithCodeActionRuntime(
        CodeActionRuntime codeActionRuntime,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return CreateCoordinatorWithCodeActionRuntime(new WorkspaceRuntimeOptions(), codeActionRuntime, toolExecutionServices);
    }

    internal static IToolExecutionContextFactory CreateCoordinatorWithCodeActionRuntime(
        WorkspaceRuntimeOptions options,
        CodeActionRuntime codeActionRuntime,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return CreateWithCodeActionRuntime(options, codeActionRuntime, toolExecutionServices);
    }

    private static WorkspaceRuntime CreateCore(
        WorkspaceCoordinatorOptions options,
        CodeActionRuntime codeActionRuntime,
        IToolExecutionServices? toolExecutionServices)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(codeActionRuntime);

        var optionsWrapper = Options.Create(options);
        var executionServices = toolExecutionServices ?? BundledCoreToolExecutionServicesFactory.Create();
        var sessionStore = new WorkspaceSessionStore();
        var workspaceSelector = new WorkspaceSelectorService();
        var workspaceLoader = new WorkspaceLoader(
            new WorkspaceHostServicesAccessor(codeActionRuntime.WorkspaceHostServices),
            new WorkspaceProjectCompatibilityInspector());
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison();
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
        var codeActionDiscoveryService = new CodeActionDiscoveryService(codeActionRuntime);
        var codeActionResolutionService = new CodeActionResolutionService(
            codeActionDiscoveryService,
            codeActionDiagnosticService,
            codeActionDescriptorRegistry,
            codeActionTokenService);
        var codeActionOperationService = new CodeActionOperationService(
            codeActionDiagnosticService,
            codeActionDescriptorRegistry);
        var codeActionQueryWorkflow = new CodeActionQueryWorkflow(
            codeActionRuntime,
            codeActionDiscoveryService,
            codeActionDiagnosticService,
            codeActionResolutionService,
            codeActionDescriptorRegistry,
            codeActionTokenService);
        var codeActionMutationWorkflow = new CodeActionMutationWorkflow(
            codeActionRuntime,
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
            workspaceSelector,
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
            workspaceSelector,
            workspaceLoader,
            new WorkspaceRootResolver(fileSystem, pathComparison),
            workspaceChangeDetector,
            workspaceStateTransitions,
            resultFactory,
            recoveryStore,
            instanceStatusPublisher);
        var transactionService = new TransactionService(
            optionsWrapper,
            sessionStore,
            workspaceSelector,
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

    private static CodeActionRuntime CreateUnavailableCodeActionRuntime()
    {
        return new CodeActionRuntime
        {
            Status = new CodeActionRuntimeStatus
            {
                IsAvailable = false,
                Message = "Code-action composition is unavailable.",
            },
        };
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
