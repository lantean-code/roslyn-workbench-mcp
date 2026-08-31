namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Projects workspace operation outcomes into MCP tool result envelopes.
/// </summary>
internal static class WorkspaceToolResultMapper
{
    /// <summary>
    /// Converts a workspace operation result into the corresponding MCP tool result envelope.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="result">The workspace operation result to publish.</param>
    /// <param name="mapData">The projection used to convert successful workspace data to the tool response type.</param>
    /// <returns>A tool result preserving the workspace outcome, snapshot, diagnostics, and warnings.</returns>
    public static ToolResult<TTarget> Map<TSource, TTarget>(WorkspaceOperationResult<TSource> result, Func<TSource, TTarget> mapData)
    {
        var snapshot = result.Context.Snapshot;

        switch (result.Status)
        {
            case WorkspaceOperationStatus.Succeeded when result.HasData:
                var mappedData = mapData(result.Data);
                return ToolResult.Succeeded(
                    mappedData,
                    snapshot: snapshot,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Rejected when result.HasError:
                var rejectedError = MapError(result.Error);
                return ToolResult.Rejected<TTarget>(
                    rejectedError,
                    result.Error.RequiredAction,
                    snapshot: snapshot,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Conflict when result.HasError:
                var conflictError = MapError(result.Error);
                return ToolResult.Conflict<TTarget>(
                    conflictError,
                    result.Error.RequiredAction,
                    snapshot: snapshot,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Faulted when result.HasError:
                var faultError = MapError(result.Error);
                return ToolResult.Faulted<TTarget>(
                    faultError,
                    result.Error.RequiredAction,
                    snapshot: snapshot,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.NoChange:
                var noChangeData = default(TTarget);
                if (result.Data is not null)
                {
                    noChangeData = mapData(result.Data);
                }

                return ToolResult.NoChange(
                    snapshot: snapshot,
                    data: noChangeData,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            default:
                throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'.");
        }
    }

    private static ToolError MapError(WorkspaceOperationError error)
    {
        return new ToolError
        {
            Code = error.Code,
            Message = error.Message,
        };
    }
}
