using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class BundledCoreToolTestHarness
{
    public static IWorkspaceRuntime CreateInspectionCoordinator(int maxResponseBytes = 65536)
    {
        return WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = maxResponseBytes,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
    }

    public static IWorkspaceRuntime CreateBuiltInCodeActionCoordinator()
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
    }

    public static IWorkspaceRuntime CreateTestCodeActionCoordinator(TimeSpan? tokenLifetime = null)
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            TokenLifetime = tokenLifetime ?? TimeSpan.FromMinutes(5),
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        return WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
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
            return Unbox<TResponse>(lease.ShortCircuitResult);
        }

        return await target.ExecuteAsync(request, lease.Context!, CancellationToken.None);
    }

    public static async Task<ToolMutationExecutionResult> ExecuteMutationAsync<TRequest>(
        IToolExecutionContextFactory coordinator,
        string toolName,
        IMutationToolHandler<TRequest, MutationProposal> target,
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
                ProposalResult = Unbox<MutationProposal>(lease.ShortCircuitResult),
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
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        return registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
    }

    private static PluginExecutionResult<TResponse> Unbox<TResponse>(PluginExecutionResultBox result)
    {
        return new PluginExecutionResult<TResponse>
        {
            Outcome = result.Outcome,
            Data = result.Data is TResponse typedData ? typedData : default,
            Changes = result.Changes,
            Error = result.Error,
            RequiredAction = result.RequiredAction,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
        };
    }
}
