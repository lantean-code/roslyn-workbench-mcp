using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionExecutionResult<TData>
{
    public CodeActionExecutionOutcome Outcome { get; private init; }

    public TData? Data { get; private init; }

    public ChangeSummary? Changes { get; private init; }

    public CodeActionExecutionError? Error { get; private init; }

    public RequiredAction? RequiredAction { get; private init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; private init; } = [];

    public IReadOnlyList<WarningInfo> Warnings { get; private init; } = [];

    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSucceeded => Outcome == CodeActionExecutionOutcome.Succeeded;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Outcome.IsError();

    private CodeActionExecutionResult()
    {
    }

    public static CodeActionExecutionResult<TData> Success(
        TData data,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = CodeActionExecutionOutcome.Succeeded,
            Data = data,
            Changes = changes,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TData> NoChange(
        TData? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = CodeActionExecutionOutcome.NoChange,
            Data = data,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TData> Rejected(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = CodeActionExecutionOutcome.Rejected,
            Error = error,
            RequiredAction = requiredAction,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TData> Conflict(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = CodeActionExecutionOutcome.Conflict,
            Error = error,
            RequiredAction = requiredAction,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TData> Faulted(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TData>
        {
            Outcome = CodeActionExecutionOutcome.Faulted,
            Error = error,
            RequiredAction = requiredAction,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }
}
