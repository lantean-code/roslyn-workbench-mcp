namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Provides the stable code and safe message for a failed Code Action invocation.
/// </summary>
internal sealed record CodeActionExecutionError
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user-facing failure explanation.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional identifier used to correlate an unexpected failure with Host diagnostics.
    /// </summary>
    public string? CorrelationId { get; init; }
}
