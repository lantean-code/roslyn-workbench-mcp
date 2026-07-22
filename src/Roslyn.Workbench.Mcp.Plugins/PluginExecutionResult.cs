using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins;

#pragma warning disable CA1000 // Outcome factories belong with the generic result contract so plugin authors cannot construct inconsistent states accidentally.
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

    private PluginExecutionResult(
        PluginExecutionOutcome outcome,
        TResponse? data,
        ChangeSummary? changes,
        PluginExecutionError? error,
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

    /// <summary>
    /// Creates a successful plugin execution result.
    /// </summary>
    /// <param name="data">The successful response payload.</param>
    /// <param name="changes">The optional change summary.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Success(
        TResponse data,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
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
    /// <param name="data">The optional response payload.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> NoChange(
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
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Rejected(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            PluginExecutionOutcome.Rejected,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    /// <summary>
    /// Creates a conflict plugin execution result.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Conflict(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            PluginExecutionOutcome.Conflict,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    /// <summary>
    /// Creates a faulted plugin execution result.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The optional continuation hint.</param>
    /// <param name="diagnostics">The diagnostics emitted by the handler.</param>
    /// <param name="warnings">The warnings emitted by the handler.</param>
    /// <returns>The normalized result.</returns>
    public static PluginExecutionResult<TResponse> Faulted(
        PluginExecutionError error,
        RequiredAction? requiredAction = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return CreateFailure(
            PluginExecutionOutcome.Faulted,
            error,
            requiredAction,
            diagnostics,
            warnings);
    }

    private static PluginExecutionResult<TResponse> CreateFailure(
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
#pragma warning restore CA1000
