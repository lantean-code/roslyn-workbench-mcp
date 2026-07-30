using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.CodeActions.References;

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

    internal ValueTask<CodeActionExecutionResult<PrepareFixAllData>> PrepareFixAllAsync(
        PrepareFixAllRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync<PrepareFixAllTool, PrepareFixAllRequest, PrepareFixAllData>(request, cancellationToken);
    }

    internal ValueTask<CodeActionExecutionResult<MutationData>> StageCodeActionAsync(
        StageCodeActionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteMutationAsync<StageCodeActionTool, StageCodeActionRequest>("stage-code-action", request, cancellationToken);
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
        where TRequest : WorkspaceMutationRequest
    {
        await using var lease = _workspace.CodeActionContextFactory.CreateMutationContext(request, cancellationToken);
        if (lease.HasFailure)
        {
            return MapFailure<MutationData>(lease.Failure);
        }

        var handler = _workspace.CreateInstance<THandler>();
        var proposal = await handler.ExecuteAsync(request, lease.Context, cancellationToken);
        if (proposal.HasError)
        {
            return proposal.Outcome switch
            {
                CodeActionExecutionOutcome.Rejected => CodeActionExecutionResult.Rejected<MutationData>(
                    proposal.Error,
                    proposal.RequiredAction,
                    proposal.Diagnostics,
                    proposal.Warnings),
                CodeActionExecutionOutcome.Conflict => CodeActionExecutionResult.Conflict<MutationData>(
                    proposal.Error,
                    proposal.RequiredAction,
                    proposal.Diagnostics,
                    proposal.Warnings),
                CodeActionExecutionOutcome.Faulted => CodeActionExecutionResult.Faulted<MutationData>(
                    proposal.Error,
                    proposal.RequiredAction,
                    proposal.Diagnostics,
                    proposal.Warnings),
                _ => throw new InvalidOperationException($"Outcome '{proposal.Outcome}' is not a failure outcome."),
            };
        }

        if (!proposal.IsSucceeded)
        {
            return CodeActionExecutionResult.NoChange<MutationData>(
                diagnostics: proposal.Diagnostics,
                warnings: proposal.Warnings);
        }

        var result = await lease.StageAsync(
            operationName,
            proposal.Data,
            proposal.Diagnostics,
            proposal.Warnings,
            cancellationToken);

        if (result.IsSucceeded && request is ICodeActionReferenceRequest referenceRequest)
        {
            _workspace.GetRequiredService<ICodeActionReferenceStore>().Remove(referenceRequest.ActionId);
        }

        return result;
    }

    private static CodeActionExecutionResult<TData> MapFailure<TData>(CodeActionExecutionFailure failure)
    {
        return failure.Outcome switch
        {
            CodeActionExecutionOutcome.Rejected => CodeActionExecutionResult.Rejected<TData>(
                failure.Error,
                failure.RequiredAction),
            CodeActionExecutionOutcome.Conflict => CodeActionExecutionResult.Conflict<TData>(
                failure.Error,
                failure.RequiredAction),
            CodeActionExecutionOutcome.Faulted => CodeActionExecutionResult.Faulted<TData>(
                failure.Error,
                failure.RequiredAction),
            _ => throw new InvalidOperationException($"Outcome '{failure.Outcome}' is not a failure outcome."),
        };
    }
}
