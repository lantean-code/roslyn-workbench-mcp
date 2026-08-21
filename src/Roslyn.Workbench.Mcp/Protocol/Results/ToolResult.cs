namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Represents the common structured envelope returned by a tool.
/// </summary>
/// <typeparam name="TData">The tool-specific data payload type.</typeparam>
internal sealed record ToolResult<TData>
{
    /// <summary>
    /// Gets the outcome of the tool invocation.
    /// </summary>
    public ToolOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the exact immutable workspace snapshot, when available.
    /// </summary>
    public SnapshotPrecondition? Snapshot { get; init; }

    /// <summary>
    /// Gets the tool-specific payload.
    /// </summary>
    public TData? Data { get; init; }

    /// <summary>
    /// Gets the optional top-level change summary.
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
    /// Gets the optional continuation hint for the caller.
    /// </summary>
    public RequiredAction? RequiredAction { get; init; }
}

/// <summary>
/// Creates structured tool result envelopes.
/// </summary>
internal static class ToolResult
{
    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="data">The tool-specific payload.</param>
    /// <param name="snapshot">The exact immutable workspace snapshot.</param>
    /// <param name="changes">The optional change summary.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A successful tool result.</returns>
    public static ToolResult<TData> Succeeded<TData>(
        TData data,
        SnapshotPrecondition? snapshot = null,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Succeeded,
            Snapshot = snapshot,
            Data = data,
            Changes = changes,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    /// <summary>
    /// Creates a no-change tool result.
    /// </summary>
    /// <param name="snapshot">The exact immutable workspace snapshot.</param>
    /// <param name="data">The optional tool-specific payload.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A no-change tool result.</returns>
    public static ToolResult<TData> NoChange<TData>(
        SnapshotPrecondition? snapshot = null,
        TData? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.NoChange,
            Snapshot = snapshot,
            Data = data,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    /// <summary>
    /// Creates a rejected tool result.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The continuation hint.</param>
    /// <param name="snapshot">The exact immutable workspace snapshot.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A rejected tool result.</returns>
    public static ToolResult<TData> Rejected<TData>(
        ToolError error,
        RequiredAction? requiredAction = null,
        SnapshotPrecondition? snapshot = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Rejected,
            Snapshot = snapshot,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
            Error = error,
            RequiredAction = requiredAction,
        };
    }

    /// <summary>
    /// Creates a conflicted tool result.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The continuation hint.</param>
    /// <param name="snapshot">The exact immutable workspace snapshot.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A conflicted tool result.</returns>
    public static ToolResult<TData> Conflict<TData>(
        ToolError error,
        RequiredAction? requiredAction = null,
        SnapshotPrecondition? snapshot = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Conflict,
            Snapshot = snapshot,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
            Error = error,
            RequiredAction = requiredAction,
        };
    }

    /// <summary>
    /// Creates a faulted tool result.
    /// </summary>
    /// <param name="error">The structured error payload.</param>
    /// <param name="requiredAction">The continuation hint.</param>
    /// <param name="snapshot">The exact immutable workspace snapshot.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A faulted tool result.</returns>
    public static ToolResult<TData> Faulted<TData>(
        ToolError error,
        RequiredAction? requiredAction = null,
        SnapshotPrecondition? snapshot = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Faulted,
            Snapshot = snapshot,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
            Error = error,
            RequiredAction = requiredAction,
        };
    }
}
