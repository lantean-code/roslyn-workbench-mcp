using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class WorkspaceCoordinatorFactory
{
    public static WorkspaceRuntime Create(
        WorkspaceCoordinatorOptions options,
        CodeActionRuntime? codeActionRuntime = null,
        IToolExecutionServices? toolExecutionServices = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtime = codeActionRuntime ?? new CodeActionRuntime
        {
            CodeActionService = new UnavailableCodeActionService(),
        };

        var optionsWrapper = Options.Create(options);
        var executionServices = toolExecutionServices ?? new UnavailableToolExecutionServices();
        var sessionStore = new WorkspaceSessionStore();
        var workspaceSelector = new WorkspaceSelectorService();
        var workspaceLoader = new WorkspaceLoader(runtime);
        var workspaceChangeDetector = new WorkspaceChangeDetector();
        var workspaceStateTransitions = new WorkspaceStateTransitions();
        var resultFactory = new WorkspaceOperationResultFactory();
        var snapshotGuard = new SnapshotGuard();
        var mutationStagingService = new MutationStagingService(sessionStore);
        var transactionCommitService = new TransactionCommitService(
            optionsWrapper,
            sessionStore,
            workspaceChangeDetector,
            workspaceStateTransitions,
            snapshotGuard,
            resultFactory);
        var coordinator = new WorkspaceExecutionContextFactory(
            optionsWrapper,
            runtime,
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

    public static IToolExecutionContextFactory CreateCoordinator(
        WorkspaceCoordinatorOptions options,
        CodeActionRuntime? codeActionRuntime = null,
        IToolExecutionServices? toolExecutionServices = null)
    {
        return Create(options, codeActionRuntime, toolExecutionServices);
    }
}
