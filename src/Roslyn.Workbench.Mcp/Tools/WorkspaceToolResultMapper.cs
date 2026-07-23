namespace Roslyn.Workbench.Mcp.Tools;

internal static class WorkspaceToolResultMapper
{
    public static ToolResult<TTarget> Map<TSource, TTarget>(WorkspaceOperationResult<TSource> result, Func<TSource, TTarget> mapData)
    {
        var workspaceId = result.Context.WorkspaceId;
        var workspaceEpoch = result.Context.WorkspaceEpoch;
        var transactionRevision = result.Context.TransactionRevision;

        switch (result.Status)
        {
            case WorkspaceOperationStatus.Succeeded when result.HasData:
                var mappedData = mapData(result.Data);
                return ToolResult<TTarget>.Succeeded(
                    mappedData,
                    workspaceId: workspaceId,
                    workspaceEpoch: workspaceEpoch,
                    transactionRevision: transactionRevision,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Rejected when result.HasError:
                var rejectedError = MapError(result.Error);
                return ToolResult<TTarget>.Rejected(
                    rejectedError,
                    result.Error.RequiredAction,
                    workspaceId: workspaceId,
                    workspaceEpoch: workspaceEpoch,
                    transactionRevision: transactionRevision,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Conflict when result.HasError:
                var conflictError = MapError(result.Error);
                return ToolResult<TTarget>.Conflict(
                    conflictError,
                    result.Error.RequiredAction,
                    workspaceId: workspaceId,
                    workspaceEpoch: workspaceEpoch,
                    transactionRevision: transactionRevision,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.Faulted when result.HasError:
                var faultError = MapError(result.Error);
                return ToolResult<TTarget>.Faulted(
                    faultError,
                    result.Error.RequiredAction,
                    workspaceId: workspaceId,
                    workspaceEpoch: workspaceEpoch,
                    transactionRevision: transactionRevision,
                    diagnostics: result.Diagnostics,
                    warnings: result.Warnings);

            case WorkspaceOperationStatus.NoChange:
                var noChangeData = default(TTarget);
                if (result.Data is not null)
                {
                    noChangeData = mapData(result.Data);
                }

                return ToolResult<TTarget>.NoChange(
                    workspaceId: workspaceId,
                    workspaceEpoch: workspaceEpoch,
                    transactionRevision: transactionRevision,
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
