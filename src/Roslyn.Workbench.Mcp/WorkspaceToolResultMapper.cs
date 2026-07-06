using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp;

internal static class WorkspaceToolResultMapper
{
    public static ToolResult<TTarget> Map<TSource, TTarget>(WorkspaceOperationResult<TSource> result, Func<TSource, TTarget> mapData)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapData);

        var workspaceId = result.Context.WorkspaceId;
        var workspaceEpoch = result.Context.WorkspaceEpoch;
        var transactionRevision = result.Context.TransactionRevision;

        return result.Status switch
        {
            WorkspaceOperationStatus.Succeeded => ToolResult<TTarget>.Succeeded(
                mapData(result.Data!),
                workspaceId: workspaceId,
                workspaceEpoch: workspaceEpoch,
                transactionRevision: transactionRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            WorkspaceOperationStatus.Rejected => ToolResult<TTarget>.Rejected(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                workspaceId: workspaceId,
                workspaceEpoch: workspaceEpoch,
                transactionRevision: transactionRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            WorkspaceOperationStatus.Conflict => ToolResult<TTarget>.Conflict(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                workspaceId: workspaceId,
                workspaceEpoch: workspaceEpoch,
                transactionRevision: transactionRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            WorkspaceOperationStatus.Faulted => ToolResult<TTarget>.Faulted(
                MapError(result.Error!),
                result.Error!.RequiredAction,
                workspaceId: workspaceId,
                workspaceEpoch: workspaceEpoch,
                transactionRevision: transactionRevision,
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            WorkspaceOperationStatus.NoChange => ToolResult<TTarget>.NoChange(
                workspaceId: workspaceId,
                workspaceEpoch: workspaceEpoch,
                transactionRevision: transactionRevision,
                data: result.Data is null ? default : mapData(result.Data),
                diagnostics: result.Diagnostics,
                warnings: result.Warnings),
            _ => throw new InvalidOperationException($"Unsupported workspace operation status '{result.Status}'."),
        };
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
