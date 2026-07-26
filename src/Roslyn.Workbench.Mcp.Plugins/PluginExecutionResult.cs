using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Represents the normalized outcome returned by a plugin handler before host mapping.
/// </summary>
/// <typeparam name="TResponse">The successful response payload type.</typeparam>
public sealed record PluginExecutionResult<TResponse>
{
    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public PluginExecutionOutcome Outcome { get; }

    /// <summary>
    /// Gets the successful response payload, when present.
    /// </summary>
    public TResponse? Data { get; }

    /// <summary>
    /// Gets the top-level change summary, when present.
    /// </summary>
    public ChangeSummary? Changes { get; }

    /// <summary>
    /// Gets the structured error payload, when present.
    /// </summary>
    public PluginExecutionError? Error { get; }

    /// <summary>
    /// Gets the optional continuation hint.
    /// </summary>
    public RequiredAction? RequiredAction { get; }

    /// <summary>
    /// Gets the diagnostics emitted by the handler.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// Gets the warnings emitted by the handler.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; }

    /// <summary>
    /// Gets a value indicating whether the result completed successfully with response data.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSucceeded => Outcome == PluginExecutionOutcome.Succeeded;

    /// <summary>
    /// Gets a value indicating whether the result represents an error outcome.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Outcome.IsError();

    internal PluginExecutionResult(
        PluginExecutionOutcome outcome,
        TResponse? data,
        ChangeSummary? changes,
        PluginExecutionError? error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings)
    {
        if (outcome == PluginExecutionOutcome.Succeeded)
        {
            ArgumentNullException.ThrowIfNull(data);
        }

        if (outcome.IsError())
        {
            ArgumentNullException.ThrowIfNull(error);
        }

        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(warnings);

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
/// Creates normalized plugin execution results.
/// </summary>
public static class PluginExecutionResult
{
    /// <summary>
    /// Creates a successful plugin execution result.
    /// </summary>
    /// <typeparam name="TResponse">The successful response payload type.</typeparam>
    /// <param name="data">The successful response payload.</param>
    /// <param name="changes">The optional change summary.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Success<TResponse>(
        TResponse data,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new PluginExecutionResult<TResponse>(
            PluginExecutionOutcome.Succeeded,
            data,
            changes,
            error: null,
            requiredAction: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a no-change plugin execution result.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="data">The optional response payload.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> NoChange<TResponse>(
        TResponse? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new PluginExecutionResult<TResponse>(
            PluginExecutionOutcome.NoChange,
            data,
            changes: null,
            error: null,
            requiredAction: null,
            diagnostics ?? [],
            warnings ?? []);
    }

    /// <summary>
    /// Creates a rejected plugin execution result.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Rejected<TResponse>(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TResponse>(
            PluginExecutionOutcome.Rejected,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    /// <summary>
    /// Creates a conflict plugin execution result.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Conflict<TResponse>(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TResponse>(
            PluginExecutionOutcome.Conflict,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    /// <summary>
    /// Creates a faulted plugin execution result.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Faulted<TResponse>(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure<TResponse>(
            PluginExecutionOutcome.Faulted,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    internal static PluginExecutionResult<TResponse> Rejected<TResponse>(
        string code,
        string message,
        RequiredAction? requiredAction = null)
    {
        var error = new PluginExecutionError
        {
            Code = code,
            Message = message,
        };

        return Rejected<TResponse>(error, requiredAction);
    }

    private static PluginExecutionResult<TResponse> CreateFailure<TResponse>(
        PluginExecutionOutcome outcome,
        PluginExecutionError error,
        RequiredAction? requiredAction,
        IReadOnlyList<DiagnosticInfo>? diagnostics,
        IReadOnlyList<WarningInfo>? warnings)
    {
        return new PluginExecutionResult<TResponse>(
            outcome,
            data: default,
            changes: null,
            error,
            requiredAction,
            diagnostics ?? [],
            warnings ?? []);
    }
}
