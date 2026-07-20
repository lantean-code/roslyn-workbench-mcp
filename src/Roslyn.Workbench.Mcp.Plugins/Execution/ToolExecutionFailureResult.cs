namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Represents a host-generated failure result that short-circuits plugin execution.
/// </summary>
internal sealed record ToolExecutionFailureResult
{
    /// <summary>
    /// Gets the normalized tool outcome.
    /// </summary>
    public PluginExecutionOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the structured error payload.
    /// </summary>
    public PluginExecutionError Error { get; init; } = new();

    /// <summary>
    /// Gets the optional continuation hint.
    /// </summary>
    public RequiredAction? RequiredAction { get; init; }

    /// <summary>
    /// Gets the diagnostics emitted by the host, when present.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the warnings emitted by the host, when present.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a normalized faulted result for an unhandled tool exception.
    /// </summary>
    /// <returns>The faulted failure result.</returns>
    public static ToolExecutionFailureResult CreateUnhandledException()
    {
        return new ToolExecutionFailureResult
        {
            Outcome = PluginExecutionOutcome.Faulted,
            Error = new PluginExecutionError
            {
                Code = "UnhandledException",
                Message = "Tool execution failed.",
                CorrelationId = Guid.NewGuid().ToString("n"),
            },
        };
    }

    /// <summary>
    /// Creates a typed plugin execution result from this failure.
    /// </summary>
    /// <typeparam name="TResponse">The response payload type.</typeparam>
    /// <returns>The typed plugin execution result.</returns>
    public PluginExecutionResult<TResponse> ToPluginExecutionResult<TResponse>()
    {
        return Outcome switch
        {
            PluginExecutionOutcome.Rejected => PluginExecutionResult<TResponse>.Rejected(
                Error,
                RequiredAction,
                Diagnostics,
                Warnings),
            PluginExecutionOutcome.Conflict => PluginExecutionResult<TResponse>.Conflict(
                Error,
                RequiredAction,
                Diagnostics,
                Warnings),
            PluginExecutionOutcome.Faulted => PluginExecutionResult<TResponse>.Faulted(
                Error,
                RequiredAction,
                Diagnostics,
                Warnings),
            _ => throw new InvalidOperationException($"Outcome '{Outcome}' is not a failure outcome."),
        };
    }
}
