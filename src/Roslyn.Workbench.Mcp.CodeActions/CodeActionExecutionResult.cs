namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed record CodeActionExecutionResult<TResponse>
{
    public CodeActionExecutionOutcome Outcome { get; init; }

    public TResponse? Data { get; init; }

    public ChangeSummary? Changes { get; init; }

    public CodeActionExecutionError? Error { get; init; }

    public RequiredAction? RequiredAction { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    public static CodeActionExecutionResult<TResponse> Success(
        TResponse data,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TResponse>
        {
            Outcome = CodeActionExecutionOutcome.Succeeded,
            Data = data,
            Changes = changes,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TResponse> NoChange(
        TResponse? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TResponse>
        {
            Outcome = CodeActionExecutionOutcome.NoChange,
            Data = data,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TResponse> Rejected(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TResponse>
        {
            Outcome = CodeActionExecutionOutcome.Rejected,
            Error = error,
            RequiredAction = requiredAction,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    public static CodeActionExecutionResult<TResponse> Conflict(
        CodeActionExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new CodeActionExecutionResult<TResponse>
        {
            Outcome = CodeActionExecutionOutcome.Conflict,
            Error = error,
            RequiredAction = requiredAction,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }
}
