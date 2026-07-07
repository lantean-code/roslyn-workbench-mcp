using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

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
        var executionServices = toolExecutionServices ?? new UnavailableToolExecutionServices();
        var sessionStore = new WorkspaceSessionStore();
        var workspaceSelector = new WorkspaceSelectorService();
        var workspaceLoader = new WorkspaceLoader(new WorkspaceHostServicesAccessor(codeActionRuntime.WorkspaceHostServices));
        var workspaceChangeDetector = new WorkspaceChangeDetector();
        var workspaceStateTransitions = new WorkspaceStateTransitions();
        var resultFactory = new WorkspaceOperationResultFactory();
        var snapshotGuard = new SnapshotGuard();
        var mutationStagingService = new MutationStagingService(new WorkspaceOperationResultFactory(), sessionStore);
        var transactionCommitService = new TransactionCommitService(
            optionsWrapper,
            sessionStore,
            workspaceChangeDetector,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory);
        var coordinator = new WorkspaceExecutionContextFactory(
            optionsWrapper,
            codeActionRuntime.QueryWorkflow,
            codeActionRuntime.MutationWorkflow,
            executionServices,
            sessionStore,
            workspaceSelector,
            workspaceChangeDetector,
            workspaceStateTransitions,
            mutationStagingService);
        var workspaceLifecycleService = new WorkspaceLifecycleService(
            optionsWrapper,
            sessionStore,
            workspaceSelector,
            workspaceLoader,
            workspaceChangeDetector,
            workspaceStateTransitions,
            resultFactory);
        var transactionService = new TransactionService(
            optionsWrapper,
            sessionStore,
            workspaceSelector,
            workspaceChangeDetector,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory,
            transactionCommitService);

        return new WorkspaceRuntime(coordinator, workspaceLifecycleService, transactionService);
    }

    private static CodeActionRuntime CreateUnavailableCodeActionRuntime()
    {
        return new CodeActionRuntime
        {
            Status = new ComponentStatus
            {
                IsAvailable = false,
                Message = "Code-action composition is unavailable.",
            },
            QueryWorkflow = new UnavailableCodeActionQueryWorkflow(),
            MutationWorkflow = new UnavailableCodeActionMutationWorkflow(),
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
