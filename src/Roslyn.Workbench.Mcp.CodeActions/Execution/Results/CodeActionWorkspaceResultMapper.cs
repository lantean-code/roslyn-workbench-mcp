namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal static class CodeActionWorkspaceResultMapper
{
    public static CodeActionExecutionFailure MapFailure(WorkspaceExecutionFailure failure)
    {
        return new CodeActionExecutionFailure
        {
            Outcome = MapOutcome(failure.Status),
            Error = MapError(failure.Error),
            RequiredAction = failure.Error.RequiredAction,
        };
    }

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
        var data = new MutationData
        {
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
