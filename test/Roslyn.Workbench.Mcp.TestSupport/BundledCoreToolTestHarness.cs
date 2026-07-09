using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class BundledCoreToolTestHarness
{
    public static IWorkspaceRuntime CreateInspectionCoordinator(int defaultMaxResults = 100)
    {
        return WorkspaceCoordinatorFactory.Create(new WorkspaceRuntimeOptions
        {
            DefaultMaxResults = defaultMaxResults,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
    }

    public static IWorkspaceRuntime CreateBuiltInCodeActionCoordinator()
    {
        var runtime = new CodeActionRuntimeComposer()
            .Compose(new CodeActionRuntimeOptions
            {
                IncludeBuiltInAssemblies = true,
            });

        return WorkspaceCoordinatorFactory.CreateWithCodeActionRuntime(runtime, BundledCoreToolExecutionServicesFactory.Create());
    }

    public static IWorkspaceRuntime CreateTestCodeActionCoordinator(TimeSpan? tokenLifetime = null)
    {
        var runtime = new CodeActionRuntimeComposer()
            .Compose(new CodeActionRuntimeOptions
            {
                TokenLifetime = tokenLifetime ?? TimeSpan.FromMinutes(5),
                IncludeBuiltInAssemblies = false,
                AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
            });

        return WorkspaceCoordinatorFactory.CreateWithCodeActionRuntime(runtime, BundledCoreToolExecutionServicesFactory.Create());
    }

    public static SnapshotPrecondition CreateSnapshot(ToolResult<WorkspaceOpenData> openResult, int? transactionRevision = null)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = transactionRevision,
        };
    }

    public static async Task<PluginExecutionResult<TResponse>> ExecuteQueryAsync<TRequest, TResponse>(
        IToolExecutionContextFactory coordinator,
        string toolName,
        IQueryToolHandler<TRequest, TResponse> target,
        TRequest request)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(target);

        var registeredTool = GetRegisteredTool(toolName);
        await using var lease = coordinator.CreateQueryContext(request, CancellationToken.None);
        if (lease.ShortCircuitResult is not null)
        {
            return lease.ShortCircuitResult.ToPluginExecutionResult<TResponse>();
        }

        return await target.ExecuteAsync(request, lease.Context!, CancellationToken.None);
    }

    public static async Task<PluginExecutionResult<TValue>> ExecuteSingletonQueryAsync<TRequest, TValue>(
        IToolExecutionContextFactory coordinator,
        string toolName,
        IQueryToolHandler<TRequest, TValue> target,
        TRequest request)
        where TRequest : WorkspaceBoundRequest
    {
        var result = await ExecuteQueryAsync(coordinator, toolName, target, request);

        return new PluginExecutionResult<TValue>
        {
            Outcome = result.Outcome,
            Data = result.Data,
            Changes = result.Changes,
            Error = result.Error,
            RequiredAction = result.RequiredAction,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
        };
    }

    public static async Task<ToolMutationExecutionResult> ExecuteMutationAsync<TRequest>(
        IToolExecutionContextFactory coordinator,
        string toolName,
        IMutationToolHandler<TRequest> target,
        TRequest request)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(target);

        var registeredTool = GetRegisteredTool(toolName);
        await using var lease = coordinator.CreateMutationContext(request, CancellationToken.None);
        if (lease.ShortCircuitResult is not null)
        {
            return new ToolMutationExecutionResult
            {
                ProposalResult = lease.ShortCircuitResult.ToPluginExecutionResult<MutationProposal>(),
            };
        }

        var context = lease.Context!;
        var proposalResult = await target.ExecuteAsync(request, context, CancellationToken.None);
        if (proposalResult.Outcome != ToolOutcome.Succeeded || proposalResult.Data is null)
        {
            return new ToolMutationExecutionResult
            {
                ProposalResult = proposalResult,
            };
        }

        var stagedResult = await context.StageAsync(
            registeredTool,
            proposalResult.Data,
            proposalResult.Diagnostics,
            proposalResult.Warnings,
            CancellationToken.None);

        return new ToolMutationExecutionResult
        {
            ProposalResult = proposalResult,
            StagedResult = stagedResult,
        };
    }

    private static RegisteredTool GetRegisteredTool(string toolName)
    {
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        return registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
    }
}
