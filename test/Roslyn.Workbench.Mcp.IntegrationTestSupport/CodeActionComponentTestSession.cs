using Microsoft.Extensions.DependencyInjection;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.CodeActions.Refactorings;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class CodeActionComponentTestSession : IAsyncDisposable
{
    private readonly WorkspaceRuntime _runtime;
    private readonly ServiceProvider _serviceProvider;

    private CodeActionComponentTestSession(WorkspaceRuntime runtime, ServiceProvider serviceProvider)
    {
        _runtime = runtime;
        _serviceProvider = serviceProvider;
    }

    public static CodeActionComponentTestSession Create(IWorkspaceRuntime workspaceRuntime)
    {
        if (workspaceRuntime is not WorkspaceRuntime runtime)
        {
            throw new ArgumentException("The Code Action component session requires a WorkspaceRuntime instance.", nameof(workspaceRuntime));
        }

        var services = new ServiceCollection();
        foreach (var descriptor in runtime.CodeActionHandlerServices)
        {
            ((ICollection<ServiceDescriptor>)services).Add(descriptor);
        }

        services.AddTransient<ListCodeActionsTool>();
        services.AddTransient<DescribeCodeActionTool>();
        services.AddTransient<StageCodeActionTool>();
        services.AddTransient<StageCodeFixTool>();
        services.AddTransient<StageFixAllTool>();
        services.AddTransient<RemoveUnusedUsingsTool>();
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        return new CodeActionComponentTestSession(runtime, serviceProvider);
    }

    public ValueTask<ToolResult<CodeActionListData>> ListAsync(ListCodeActionsRequest request, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync<ListCodeActionsTool, ListCodeActionsRequest, CodeActionListData>(request, cancellationToken);
    }

    public ValueTask<ToolResult<DescribeCodeActionData>> DescribeAsync(DescribeCodeActionRequest request, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync<DescribeCodeActionTool, DescribeCodeActionRequest, DescribeCodeActionData>(request, cancellationToken);
    }

    public ValueTask<ToolResult<MutationData>> StageCodeActionAsync(StageCodeActionRequest request, CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageCodeActionTool, StageCodeActionRequest>("stage-code-action", request, cancellationToken);
    }

    public ValueTask<ToolResult<MutationData>> StageCodeFixAsync(StageCodeFixRequest request, CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageCodeFixTool, StageCodeFixRequest>("stage-code-fix", request, cancellationToken);
    }

    public ValueTask<ToolResult<MutationData>> StageFixAllAsync(StageFixAllRequest request, CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageFixAllTool, StageFixAllRequest>("stage-fix-all", request, cancellationToken);
    }

    public ValueTask<ToolResult<MutationData>> RemoveUnusedUsingsAsync(RemoveUnusedUsingsRequest request, CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<RemoveUnusedUsingsTool, RemoveUnusedUsingsRequest>("remove-unused-usings", request, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _serviceProvider.DisposeAsync();
    }

    private async ValueTask<ToolResult<TResponse>> ExecuteQueryAsync<THandler, TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        await using var lease = _runtime.CodeActionContextFactory.CreateQueryContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return MapFailure<TResponse>(lease.Failure);
        }

        var handler = _serviceProvider.GetRequiredService<THandler>();
        var result = await handler.ExecuteAsync(request, lease.Context, cancellationToken);
        return MapResult(result);
    }

    private async ValueTask<ToolResult<MutationData>> ExecuteMutationAsync<THandler, TRequest>(
        string operationName,
        TRequest request,
        CancellationToken cancellationToken)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceBoundRequest
    {
        await using var lease = _runtime.CodeActionContextFactory.CreateMutationContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return MapFailure<MutationData>(lease.Failure);
        }

        var handler = _serviceProvider.GetRequiredService<THandler>();
        var proposal = await handler.ExecuteAsync(request, lease.Context, cancellationToken);
        if (proposal.Outcome.IsError())
        {
            return MapFailure<MutationData>(new CodeActionExecutionFailure
            {
                Outcome = proposal.Outcome,
                Error = proposal.Error ?? throw new InvalidOperationException("A failed Code Action proposal must provide an error."),
                RequiredAction = proposal.RequiredAction,
            }, proposal.Diagnostics, proposal.Warnings);
        }

        if (proposal.Outcome == CodeActionExecutionOutcome.NoChange || proposal.Data is null)
        {
            return ToolResult<MutationData>.NoChange(
                diagnostics: proposal.Diagnostics,
                warnings: proposal.Warnings);
        }

        var result = await lease.StageAsync(
            operationName,
            proposal.Data,
            proposal.Diagnostics,
            proposal.Warnings,
            cancellationToken);
        return MapResult(result);
    }

    private static ToolResult<TData> MapResult<TData>(CodeActionExecutionResult<TData> result)
    {
        return result.Outcome switch
        {
            CodeActionExecutionOutcome.Succeeded => ToolResult<TData>.Succeeded(
                result.Data ?? throw new InvalidOperationException("A successful Code Action result must provide data."),
                transactionRevision: result.Data is MutationData mutation ? mutation.Transaction?.Revision : null,
                changes: result.Changes,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            CodeActionExecutionOutcome.NoChange => ToolResult<TData>.NoChange(
                data: result.Data,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => MapFailure<TData>(new CodeActionExecutionFailure
            {
                Outcome = result.Outcome,
                Error = result.Error ?? throw new InvalidOperationException("A failed Code Action result must provide an error."),
                RequiredAction = result.RequiredAction,
            }, result.Diagnostics, result.Warnings),
        };
    }

    private static ToolResult<TData> MapFailure<TData>(
        CodeActionExecutionFailure failure,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        var error = new ToolError
        {
            Code = failure.Error.Code,
            Message = failure.Error.Message,
            CorrelationId = failure.Error.CorrelationId,
        };

        return failure.Outcome switch
        {
            CodeActionExecutionOutcome.Rejected => ToolResult<TData>.Rejected(error, failure.RequiredAction, diagnostics: diagnostics, warnings: warnings),
            CodeActionExecutionOutcome.Conflict => ToolResult<TData>.Conflict(error, failure.RequiredAction, diagnostics: diagnostics, warnings: warnings),
            CodeActionExecutionOutcome.Faulted => ToolResult<TData>.Faulted(error, failure.RequiredAction, diagnostics: diagnostics, warnings: warnings),
            _ => throw new InvalidOperationException($"Outcome '{failure.Outcome}' is not a Code Action failure."),
        };
    }
}
