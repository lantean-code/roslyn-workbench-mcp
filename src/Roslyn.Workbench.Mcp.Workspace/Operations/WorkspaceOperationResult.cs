using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationResult<TOutcome>
{
    public WorkspaceOperationStatus Status { get; }

    public WorkspaceOperationContext Context { get; }

    public TOutcome? Data { get; }

    public WorkspaceOperationError? Error { get; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    public IReadOnlyList<WarningInfo> Warnings { get; }

    [MemberNotNullWhen(true, nameof(Data))]
    public bool HasData => Data is not null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;

    internal WorkspaceOperationResult(
        WorkspaceOperationStatus status,
        WorkspaceOperationContext context,
        TOutcome? data,
        WorkspaceOperationError? error,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        Status = status;
        Context = context;
        Data = data;
        Error = error;
        Diagnostics = diagnostics;
        Warnings = warnings;
    }
}

internal static class WorkspaceOperationResult
{
    public static WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>(
            WorkspaceOperationStatus.Succeeded,
            context ?? new WorkspaceOperationContext(),
            data,
            error: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    public static WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        TOutcome? data = default,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>(
            WorkspaceOperationStatus.NoChange,
            context ?? new WorkspaceOperationContext(),
            data,
            error: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    public static WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Rejected, error, context, diagnostics, warnings);
    }

    public static WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Conflict, error, context, diagnostics, warnings);
    }

    public static WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed<TOutcome>(WorkspaceOperationStatus.Faulted, error, context, diagnostics, warnings);
    }

    private static WorkspaceOperationResult<TOutcome> Failed<TOutcome>(
        WorkspaceOperationStatus status,
        WorkspaceOperationError error,
        WorkspaceOperationContext? context,
        IReadOnlyList<DiagnosticInfo>? diagnostics,
        IReadOnlyList<WarningInfo>? warnings)
    {
        return new WorkspaceOperationResult<TOutcome>(
            status,
            context ?? new WorkspaceOperationContext(),
            data: default,
            error,
            diagnostics ?? [],
            warnings ?? []);
    }
}
