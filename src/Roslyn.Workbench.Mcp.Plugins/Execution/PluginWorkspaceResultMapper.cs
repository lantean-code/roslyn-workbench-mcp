namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal static class PluginWorkspaceResultMapper
{
    public static ToolExecutionFailureResult? MapFailure(WorkspaceExecutionFailure? failure)
    {
        if (failure is null)
        {
            return null;
        }

        return new ToolExecutionFailureResult
        {
            Outcome = MapOutcome(failure.Status),
            Error = new PluginExecutionError
            {
                Code = failure.Error.Code,
                Message = failure.Error.Message,
            },
            RequiredAction = failure.Error.RequiredAction,
        };
    }

    public static PluginExecutionResult<MutationData> MapMutation(
        WorkspaceOperationResult<MutationStagingOutcome> result)
    {
        return result.Status switch
        {
            WorkspaceOperationStatus.Succeeded => PluginExecutionResult<MutationData>.Success(
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
            WorkspaceOperationStatus.Rejected => PluginExecutionResult<MutationData>.Rejected(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict => PluginExecutionResult<MutationData>.Conflict(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted => new PluginExecutionResult<MutationData>
            {
                Outcome = PluginExecutionOutcome.Faulted,
                Error = MapError(result.Error!),
                RequiredAction = result.Error!.RequiredAction,
                Diagnostics = result.Diagnostics,
                Warnings = result.Warnings,
            },
            WorkspaceOperationStatus.NoChange => PluginExecutionResult<MutationData>.NoChange(
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
    }

    private static PluginExecutionOutcome MapOutcome(WorkspaceOperationStatus status)
    {
        return status switch
        {
            WorkspaceOperationStatus.Rejected => PluginExecutionOutcome.Rejected,
            WorkspaceOperationStatus.Conflict => PluginExecutionOutcome.Conflict,
            WorkspaceOperationStatus.Faulted => PluginExecutionOutcome.Faulted,
            _ => throw new InvalidOperationException($"Workspace status '{status}' is not a failure status."),
        };
    }

    private static PluginExecutionError MapError(WorkspaceOperationError error)
    {
        return new PluginExecutionError
        {
            Code = error.Code,
            Message = error.Message,
        };
    }
}
