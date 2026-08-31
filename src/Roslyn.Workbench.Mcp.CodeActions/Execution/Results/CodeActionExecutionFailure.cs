namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Carries a normalized Code Action failure and any recovery guidance or diagnostics.
/// </summary>
internal sealed record CodeActionExecutionFailure
{
    /// <summary>
    /// Gets the failure category exposed to the MCP execution layer.
    /// </summary>
    public CodeActionExecutionOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the structured error details.
    /// </summary>
    public CodeActionExecutionError Error { get; init; } = new();

    /// <summary>
    /// Gets the action required before the request can continue.
    /// </summary>
    public RequiredAction? RequiredAction { get; init; }

    /// <summary>
    /// Gets diagnostics relevant to the failed operation.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets non-fatal warnings produced before the failure.
    /// </summary>
    public IReadOnlyList<WarningInfo> Warnings { get; init; } = [];
}
