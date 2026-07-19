using Roslyn.Workbench.Mcp.Protocol.Validation;

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
    /// Gets the effective workspace identifier, when available.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the current workspace epoch, when available.
    /// </summary>
    public long? WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the current transaction revision, when available.
    /// </summary>
    public int? TransactionRevision { get; init; }

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

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="data">The tool-specific payload.</param>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch.</param>
    /// <param name="transactionRevision">The transaction revision.</param>
    /// <param name="changes">The optional change summary.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A successful tool result.</returns>
    public static ToolResult<TData> Succeeded(
        TData data,
        string? workspaceId = null,
        long? workspaceEpoch = null,
        int? transactionRevision = null,
        ChangeSummary? changes = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Succeeded,
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            TransactionRevision = transactionRevision,
            Data = data,
            Changes = changes,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
        };
    }

    /// <summary>
    /// Creates a no-change tool result.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch.</param>
    /// <param name="transactionRevision">The transaction revision.</param>
    /// <param name="data">The optional tool-specific payload.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A no-change tool result.</returns>
    public static ToolResult<TData> NoChange(
        string? workspaceId = null,
        long? workspaceEpoch = null,
        int? transactionRevision = null,
        TData? data = default,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.NoChange,
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            TransactionRevision = transactionRevision,
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
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch.</param>
    /// <param name="transactionRevision">The transaction revision.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A rejected tool result.</returns>
    public static ToolResult<TData> Rejected(
        ToolError error,
        RequiredAction? requiredAction = null,
        string? workspaceId = null,
        long? workspaceEpoch = null,
        int? transactionRevision = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Rejected,
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            TransactionRevision = transactionRevision,
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
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch.</param>
    /// <param name="transactionRevision">The transaction revision.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A conflicted tool result.</returns>
    public static ToolResult<TData> Conflict(
        ToolError error,
        RequiredAction? requiredAction = null,
        string? workspaceId = null,
        long? workspaceEpoch = null,
        int? transactionRevision = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Conflict,
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            TransactionRevision = transactionRevision,
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
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch.</param>
    /// <param name="transactionRevision">The transaction revision.</param>
    /// <param name="diagnostics">The diagnostics emitted by the tool.</param>
    /// <param name="warnings">The warnings emitted by the tool.</param>
    /// <returns>A faulted tool result.</returns>
    public static ToolResult<TData> Faulted(
        ToolError error,
        RequiredAction? requiredAction = null,
        string? workspaceId = null,
        long? workspaceEpoch = null,
        int? transactionRevision = null,
        IReadOnlyList<DiagnosticInfo>? diagnostics = null,
        IReadOnlyList<WarningInfo>? warnings = null)
    {
        return new ToolResult<TData>
        {
            Outcome = ToolOutcome.Faulted,
            WorkspaceId = workspaceId,
            WorkspaceEpoch = workspaceEpoch,
            TransactionRevision = transactionRevision,
            Diagnostics = diagnostics ?? [],
            Warnings = warnings ?? [],
            Error = error,
            RequiredAction = requiredAction,
        };
    }

    /// <summary>
    /// Validates the envelope shape against the shared contract invariants.
    /// </summary>
    /// <returns>The validation errors, if any.</returns>
    public IReadOnlyList<string> Validate()
    {
        return ContractValidator.Validate(this);
    }
}
