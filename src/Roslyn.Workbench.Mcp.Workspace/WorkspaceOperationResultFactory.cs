using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceOperationResultFactory : IWorkspaceOperationResultFactory
{
    public WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>
        {
            Status = WorkspaceOperationStatus.Succeeded,
            Context = context ?? new WorkspaceOperationContext(),
            Data = data,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Rejected<TOutcome>(CreateError(code, message, requiredAction), context, diagnostics, warnings);
    }

    public WorkspaceOperationResult<TOutcome> Rejected<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Create<TOutcome>(WorkspaceOperationStatus.Rejected, context, error, diagnostics, warnings);
    }

    public WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Conflict<TOutcome>(CreateError(code, message, requiredAction), context, diagnostics, warnings);
    }

    public WorkspaceOperationResult<TOutcome> Conflict<TOutcome>(
        WorkspaceOperationError error,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Create<TOutcome>(WorkspaceOperationStatus.Conflict, context, error, diagnostics, warnings);
    }

    public WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return Create<TOutcome>(WorkspaceOperationStatus.Faulted, context, CreateError(code, message, requiredAction), diagnostics, warnings);
    }

    public WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        WorkspaceOperationContext? context = null,
        TOutcome? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new WorkspaceOperationResult<TOutcome>
        {
            Status = WorkspaceOperationStatus.NoChange,
            Context = context ?? new WorkspaceOperationContext(),
            Data = data,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    private static WorkspaceOperationResult<TOutcome> Create<TOutcome>(
        WorkspaceOperationStatus status,
        WorkspaceOperationContext? context,
        WorkspaceOperationError error,
        IReadOnlyList<DiagnosticInfo>? diagnostics,
        IReadOnlyList<WarningInfo>? warnings)
    {
        return new WorkspaceOperationResult<TOutcome>
        {
            Status = status,
            Context = context ?? new WorkspaceOperationContext(),
            Error = error,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    private static WorkspaceOperationError CreateError(string code, string message, RequiredAction? requiredAction)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = message,
            RequiredAction = requiredAction,
        };
    }
}
