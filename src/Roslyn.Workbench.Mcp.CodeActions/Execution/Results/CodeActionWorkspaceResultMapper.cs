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
            WorkspaceOperationStatus.Succeeded when result.HasData => CodeActionExecutionResult<MutationData>.Success(
                new MutationData
                {
                    Operation = result.Data.Operation,
                    Summary = result.Data.Summary,
                    Transaction = result.Data.Transaction,
                    Preview = result.Data.Preview,
                },
                result.Data.Changes,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Rejected when result.HasError => CodeActionExecutionResult<MutationData>.Rejected(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict when result.HasError => CodeActionExecutionResult<MutationData>.Conflict(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted when result.HasError => CodeActionExecutionResult<MutationData>.Faulted(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.NoChange => CodeActionExecutionResult<MutationData>.NoChange(
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
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
