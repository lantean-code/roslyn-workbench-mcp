using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.CodeActions.Refactorings;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed class CodeActionComponentTestSession
{
    private readonly ComponentWorkspace _workspace;

    internal CodeActionComponentTestSession(ComponentWorkspace workspace)
    {
        _workspace = workspace;
    }

    internal ValueTask<CodeActionExecutionResult<CodeActionListData>> ListAsync(
        ListCodeActionsRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync<ListCodeActionsTool, ListCodeActionsRequest, CodeActionListData>(request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> DescribeAsync(
        DescribeCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync<DescribeCodeActionTool, DescribeCodeActionRequest, DescribeCodeActionData>(request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<MutationData>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageCodeActionTool, StageCodeActionRequest>("stage-code-action", request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<MutationData>> StageCodeFixAsync(
        StageCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageCodeFixTool, StageCodeFixRequest>("stage-code-fix", request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<MutationData>> StageFixAllAsync(
        StageFixAllRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageFixAllTool, StageFixAllRequest>("stage-fix-all", request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<MutationData>> RemoveUnusedUsingsAsync(
        RemoveUnusedUsingsRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<RemoveUnusedUsingsTool, RemoveUnusedUsingsRequest>("remove-unused-usings", request, cancellationToken);
    }

    private async ValueTask<CodeActionExecutionResult<TResponse>> ExecuteQueryAsync<THandler, TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        await using var lease = _workspace.CodeActionContextFactory.CreateQueryContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return MapFailure<TResponse>(lease.Failure);
        }

        var handler = _workspace.CreateInstance<THandler>();
        return await handler.ExecuteAsync(request, lease.Context, cancellationToken);
    }

    internal async ValueTask<CodeActionExecutionResult<MutationData>> ExecuteMutationAsync<THandler, TRequest>(
        string operationName,
        TRequest request,
        CancellationToken cancellationToken)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceBoundRequest
    {
        await using var lease = _workspace.CodeActionContextFactory.CreateMutationContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return MapFailure<MutationData>(lease.Failure);
        }

        var handler = _workspace.CreateInstance<THandler>();
        var proposal = await handler.ExecuteAsync(request, lease.Context, cancellationToken);
        if (proposal.Outcome.IsError())
        {
            return new CodeActionExecutionResult<MutationData>
            {
                Outcome = proposal.Outcome,
                Error = proposal.Error ?? throw new InvalidOperationException("A failed Code Action proposal must provide an error."),
                RequiredAction = proposal.RequiredAction,
                Diagnostics = proposal.Diagnostics,
                Warnings = proposal.Warnings,
            };
        }

        if (proposal.Outcome == CodeActionExecutionOutcome.NoChange || proposal.Data is null)
        {
            return CodeActionExecutionResult<MutationData>.NoChange(
                diagnostics: proposal.Diagnostics,
                warnings: proposal.Warnings);
        }

        return await lease.StageAsync(
            operationName,
            proposal.Data,
            proposal.Diagnostics,
            proposal.Warnings,
            cancellationToken);
    }

    private static CodeActionExecutionResult<TData> MapFailure<TData>(CodeActionExecutionFailure failure)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = failure.Outcome,
            Error = failure.Error,
            RequiredAction = failure.RequiredAction,
        };
    }
}
