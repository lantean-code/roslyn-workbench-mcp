namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal sealed class WorkspaceOperationResultFactory : IWorkspaceOperationResultFactory
{
    public WorkspaceOperationResult<TOutcome> Succeeded<TOutcome>(
        TOutcome data,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult<TOutcome>.Succeeded(
            data,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
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
        return WorkspaceOperationResult<TOutcome>.Rejected(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
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
        return WorkspaceOperationResult<TOutcome>.Conflict(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    public WorkspaceOperationResult<TOutcome> Faulted<TOutcome>(
        string code,
        string message,
        RequiredAction? requiredAction = null,
        WorkspaceOperationContext? context = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        var error = CreateError(code, message, requiredAction);

        return WorkspaceOperationResult<TOutcome>.Faulted(
            error,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
    }

    public WorkspaceOperationResult<TOutcome> NoChange<TOutcome>(
        WorkspaceOperationContext? context = null,
        TOutcome? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return WorkspaceOperationResult<TOutcome>.NoChange(
            data,
            context ?? new WorkspaceOperationContext(),
            diagnostics ?? [],
            warnings ?? []);
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
