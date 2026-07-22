using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal sealed record CodeActionExecutionResult<TData>
{
    public CodeActionExecutionOutcome Outcome { get; }

    public TData? Data { get; }

    public ChangeSummary? Changes { get; }

    public CodeActionExecutionError? Error { get; }

    public RequiredAction? RequiredAction { get; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    public IReadOnlyList<WarningInfo> Warnings { get; }

    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSucceeded => Outcome == CodeActionExecutionOutcome.Succeeded;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Outcome.IsError();

    private CodeActionExecutionResult(
        CodeActionExecutionOutcome outcome,
        TData? data,
        ChangeSummary? changes,
        CodeActionExecutionError? error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        Outcome = outcome;
        Data = data;
        Changes = changes;
        Error = error;
        RequiredAction = requiredAction;
        Diagnostics = diagnostics;
        Warnings = warnings;
    }

    public static CodeActionExecutionResult<TData> Success(
        TData data,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>(
            CodeActionExecutionOutcome.Succeeded,
            data,
            changes,
            error: null,
            requiredAction: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    public static CodeActionExecutionResult<TData> NoChange(
        TData? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>(
            CodeActionExecutionOutcome.NoChange,
            data,
            changes: null,
            error: null,
            requiredAction: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    public static CodeActionExecutionResult<TData> Rejected(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            CodeActionExecutionOutcome.Rejected,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    public static CodeActionExecutionResult<TData> Conflict(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            CodeActionExecutionOutcome.Conflict,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    public static CodeActionExecutionResult<TData> Faulted(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            CodeActionExecutionOutcome.Faulted,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    private static CodeActionExecutionResult<TData> CreateFailure(
        CodeActionExecutionOutcome outcome,
        CodeActionExecutionError error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo>? diagnostics,
        IReadOnlyList<WarningInfo>? warnings)
    {
        return new CodeActionExecutionResult<TData>(
            outcome,
            data: default,
            changes: null,
            error,
            requiredAction,
            diagnostics ?? [],
            warnings ?? []);
    }
}
