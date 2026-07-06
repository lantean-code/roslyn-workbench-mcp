using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Represents the erased execution result used at the host dispatch boundary.
/// </summary>
public sealed record PluginExecutionResultBox
{
    /// <summary>
    /// Gets the normalized tool outcome.
    /// </summary>
    public ToolOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the boxed successful payload, when present.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets the top-level change summary, when present.
    /// </summary>
    public ChangeSummary? Changes { get; init; }

    /// <summary>
    /// Gets the diagnostics emitted by the tool.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the warnings emitted by the tool.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the structured error payload, when present.
    /// </summary>
    public ToolError? Error { get; init; }

    /// <summary>
    /// Gets the optional continuation hint.
    /// </summary>
    public RequiredAction? RequiredAction { get; init; }

    /// <summary>
    /// Boxes a plugin execution result for host-side dispatch.
    /// </summary>
    /// <typeparam name="TResponse">The successful response payload type.</typeparam>
    /// <param name="result">The typed plugin execution result.</param>
    /// <returns>The boxed execution result.</returns>
    public static PluginExecutionResultBox From<TResponse>(PluginExecutionResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new PluginExecutionResultBox
        {
            Outcome = result.Outcome,
            Data = result.Data,
            Changes = result.Changes,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
            Error = result.Error,
            RequiredAction = result.RequiredAction,
        };
    }

    /// <summary>
    /// Boxes a tool result for host-side response shaping.
    /// </summary>
    /// <typeparam name="TResponse">The successful response payload type.</typeparam>
    /// <param name="result">The typed tool result.</param>
    /// <returns>The boxed execution result.</returns>
    public static PluginExecutionResultBox From<TResponse>(ToolResult<TResponse> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new PluginExecutionResultBox
        {
            Outcome = result.Outcome,
            Data = result.Data,
            Changes = result.Changes,
            Diagnostics = result.Diagnostics,
            Warnings = result.Warnings,
            Error = result.Error,
            RequiredAction = result.RequiredAction,
        };
    }

    /// <summary>
    /// Creates a normalized faulted result for an unhandled tool exception.
    /// </summary>
    /// <returns>The faulted boxed result.</returns>
    public static PluginExecutionResultBox CreateUnhandledException()
    {
        return new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Faulted,
            Error = new ToolError
            {
                Code = "UnhandledException",
                Message = "Tool execution failed.",
                CorrelationId = Guid.NewGuid().ToString("n"),
            },
        };
    }
}
