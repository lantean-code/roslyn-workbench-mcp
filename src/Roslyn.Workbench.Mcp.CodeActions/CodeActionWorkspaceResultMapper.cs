namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class CodeActionWorkspaceResultMapper
{
    public static CodeActionExecutionFailure? MapFailure(WorkspaceExecutionFailure? failure)
    {
        if (failure is null)
        {
            return null;
        }

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
            WorkspaceOperationStatus.Succeeded => CodeActionExecutionResult<MutationData>.Success(
                new MutationData
                {
                    Operation = result.Data!.Operation,
                    Summary = result.Data.Summary,
                    Transaction = result.Data.Transaction,
                    Preview = result.Data.Preview,
                },
                result.Data.Changes,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Rejected => CodeActionExecutionResult<MutationData>.Rejected(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict => CodeActionExecutionResult<MutationData>.Conflict(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted => new CodeActionExecutionResult<MutationData>
            {
                Outcome = CodeActionExecutionOutcome.Faulted,
                Error = MapError(result.Error!),
                RequiredAction = result.Error!.RequiredAction,
                Diagnostics = result.Diagnostics,
                Warnings = result.Warnings,
            },
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
