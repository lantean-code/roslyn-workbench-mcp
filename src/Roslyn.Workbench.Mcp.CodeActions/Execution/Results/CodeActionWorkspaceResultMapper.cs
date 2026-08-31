namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Projects neutral workspace acquisition and staging outcomes into Code Action execution results.
/// </summary>
internal static class CodeActionWorkspaceResultMapper
{
    /// <summary>
    /// Converts a workspace acquisition failure into a Code Action failure.
    /// </summary>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <returns>The Code Action execution failure.</returns>
    public static CodeActionExecutionFailure MapFailure(WorkspaceExecutionFailure failure)
    {
        return new CodeActionExecutionFailure
        {
            Outcome = MapOutcome(failure.Status),
            Error = MapError(failure.Error),
            RequiredAction = failure.Error.RequiredAction,
        };
    }

    /// <summary>
    /// Converts a workspace mutation staging result into a Code Action result.
    /// </summary>
    /// <param name="result">The neutral mutation staging result.</param>
    /// <returns>The corresponding Code Action success, no-change, or failure result.</returns>
    public static CodeActionExecutionResult<MutationData> MapMutation(
        WorkspaceOperationResult<MutationStagingOutcome> result)
    {
        return result.Status switch
        {
            WorkspaceOperationStatus.Succeeded when result.HasData => MapSuccess(result, result.Data),
            WorkspaceOperationStatus.Rejected when result.HasError => CodeActionExecutionResult.Rejected<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict when result.HasError => CodeActionExecutionResult.Conflict<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted when result.HasError => CodeActionExecutionResult.Faulted<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.NoChange => CodeActionExecutionResult.NoChange<MutationData>(
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
    }

    private static CodeActionExecutionResult<MutationData> MapSuccess(
        WorkspaceOperationResult<MutationStagingOutcome> result,
        MutationStagingOutcome outcome)
    {
        var snapshot = result.Context.Snapshot
            ?? throw new InvalidOperationException("A successful mutation result must identify its workspace snapshot.");

        var data = new MutationData
        {
            Snapshot = snapshot,
            Operation = outcome.Operation,
            Summary = outcome.Summary,
            Transaction = outcome.Transaction,
            Preview = outcome.Preview,
        };

        return CodeActionExecutionResult.Success(
            data,
            outcome.Changes,
            result.Diagnostics,
            result.Warnings);
    }

    private static CodeActionExecutionOutcome MapOutcome(WorkspaceOperationStatus status)
    {
        return status switch
        {
            WorkspaceOperationStatus.Rejected => CodeActionExecutionOutcome.Rejected,
            WorkspaceOperationStatus.Conflict => CodeActionExecutionOutcome.Conflict,
            WorkspaceOperationStatus.Faulted => CodeActionExecutionOutcome.Faulted,
            _ => throw new InvalidOperationException($"Workspace status '{status}' is not a failure status."),
        };
    }

    private static CodeActionExecutionError MapError(WorkspaceOperationError error)
    {
        return new CodeActionExecutionError
        {
            Code = error.Code,
            Message = error.Message,
        };
    }
}
