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

    private WorkspaceOperationResult(
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

    internal static WorkspaceOperationResult<TOutcome> Succeeded(
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

    internal static WorkspaceOperationResult<TOutcome> NoChange(
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

    internal static WorkspaceOperationResult<TOutcome> Rejected(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed(WorkspaceOperationStatus.Rejected, error, context, diagnostics, warnings);
    }

    internal static WorkspaceOperationResult<TOutcome> Conflict(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed(WorkspaceOperationStatus.Conflict, error, context, diagnostics, warnings);
    }

    internal static WorkspaceOperationResult<TOutcome> Faulted(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Failed(WorkspaceOperationStatus.Faulted, error, context, diagnostics, warnings);
    }

    private static WorkspaceOperationResult<TOutcome> Failed(
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
