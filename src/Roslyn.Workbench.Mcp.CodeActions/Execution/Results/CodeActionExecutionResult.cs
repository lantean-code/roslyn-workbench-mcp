using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Represents the normalized data, diagnostics, and recovery state produced by a Code Action handler.
/// </summary>
/// <typeparam name="TData">The data type.</typeparam>
internal sealed record CodeActionExecutionResult<TData>
{
    /// <summary>
    /// Gets the normalized execution outcome.
    /// </summary>
    public CodeActionExecutionOutcome Outcome { get; }

    /// <summary>
    /// Gets the successful response payload, when present.
    /// </summary>
    public TData? Data { get; }

    /// <summary>
    /// Gets the top-level source change summary, when present.
    /// </summary>
    public ChangeSummary? Changes { get; }

    /// <summary>
    /// Gets the structured error for a failed outcome.
    /// </summary>
    public CodeActionExecutionError? Error { get; }

    /// <summary>
    /// Gets the action required before the request can continue.
    /// </summary>
    public RequiredAction? RequiredAction { get; }

    /// <summary>
    /// Gets diagnostics produced during execution.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Gets non-fatal warnings produced during execution.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSucceeded => Outcome == CodeActionExecutionOutcome.Succeeded;

    /// <summary>
    /// Gets a value indicating whether the result contains an error.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Outcome.IsError();

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionExecutionResult{TData}"/> class.
    /// </summary>
    /// <param name="outcome">The normalized execution outcome.</param>
    /// <param name="data">The successful response payload.</param>
    /// <param name="changes">The optional top-level source change summary.</param>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
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

/// <summary>
/// Creates normalized Code Action execution results.
/// </summary>
internal static class CodeActionExecutionResult
{
    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="changes">The optional top-level source change summary.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents successful completion.</returns>
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

    /// <summary>
    /// Creates an operation result that records no change.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="data">The structured data to include in the result.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents an unchanged workspace.</returns>
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

    /// <summary>
    /// Creates a rejected operation result.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents rejection.</returns>
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

    /// <summary>
    /// Creates a conflicting operation result.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents a workspace conflict.</returns>
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

    /// <summary>
    /// Creates a faulted operation result.
    /// </summary>
    /// <typeparam name="TData">The data type.</typeparam>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    /// <returns>A result that represents failure.</returns>
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
