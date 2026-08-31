namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Maps neutral workspace failures and mutation outcomes onto plugin execution contracts.
/// </summary>
internal static class PluginWorkspaceResultMapper
{
    /// <summary>
    /// Maps a workspace acquisition failure to the plugin failure projection.
    /// </summary>
    /// <param name="failure">The neutral workspace failure.</param>
    /// <returns>The corresponding plugin execution failure.</returns>
    public static ToolExecutionFailureResult MapFailure(WorkspaceExecutionFailure failure)
    {
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

    /// <summary>
    /// Maps a workspace mutation staging result to the corresponding plugin outcome.
    /// </summary>
    /// <param name="result">The neutral mutation staging result.</param>
    /// <returns>The plugin mutation execution result.</returns>
    public static PluginExecutionResult<MutationData> MapMutation(
        WorkspaceOperationResult<MutationStagingOutcome> result)
    {
        return result.Status switch
        {
            WorkspaceOperationStatus.Succeeded when result.HasData => MapSuccess(result, result.Data),
            WorkspaceOperationStatus.Rejected when result.HasError => PluginExecutionResult.Rejected<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Conflict when result.HasError => PluginExecutionResult.Conflict<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.Faulted when result.HasError => PluginExecutionResult.Faulted<MutationData>(
                MapError(result.Error),
                result.Error.RequiredAction,
                result.Diagnostics,
                result.Warnings),
            WorkspaceOperationStatus.NoChange => PluginExecutionResult.NoChange<MutationData>(
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
    }

    private static PluginExecutionResult<MutationData> MapSuccess(
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

        return PluginExecutionResult.Success(
            data,
            outcome.Changes,
            result.Diagnostics,
            result.Warnings);
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
