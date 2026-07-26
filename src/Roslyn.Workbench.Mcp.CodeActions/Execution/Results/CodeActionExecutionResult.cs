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

    internal CodeActionExecutionResult(
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
}

internal static class CodeActionExecutionResult
{
    public static CodeActionExecutionResult<TData> Success<TData>(
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

    public static CodeActionExecutionResult<TData> NoChange<TData>(
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

    public static CodeActionExecutionResult<TData> Rejected<TData>(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TData>(
            CodeActionExecutionOutcome.Rejected,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    public static CodeActionExecutionResult<TData> Conflict<TData>(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TData>(
            CodeActionExecutionOutcome.Conflict,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    public static CodeActionExecutionResult<TData> Faulted<TData>(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TData>(
            CodeActionExecutionOutcome.Faulted,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    private static CodeActionExecutionResult<TData> CreateFailure<TData>(
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
