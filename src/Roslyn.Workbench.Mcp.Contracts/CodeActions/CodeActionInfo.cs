namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Describes one applicable code action or code fix.
/// </summary>
public sealed record CodeActionInfo
{
    /// <summary>
    /// Gets the workspace identifier for which the action is valid.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the stable provider identity.
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional action kind.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Gets the optional Roslyn equivalence key.
    /// </summary>
    public string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the nested action path used to reproduce the selection.
    /// </summary>
    public IReadOnlyList<int> ActionPath { get; init; } = [];

    /// <summary>
    /// Gets the diagnostic identifiers associated with the action.
    /// </summary>
    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    /// <summary>
    /// Gets the workspace epoch for which the action is valid.
    /// </summary>
    public long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the optional transaction revision for which the action is valid.
    /// </summary>
    public int? TransactionRevision { get; init; }

    /// <summary>
    /// Gets the expiry timestamp in UTC.
    /// </summary>
    public string ExpiresAt { get; init; } = string.Empty;

    /// <summary>
    /// Gets the execution mode for the discovered action.
    /// </summary>
    public CodeActionExecutionMode? ExecutionMode { get; init; }

    /// <summary>
    /// Gets the dedicated executor tool name when the action is parameterised.
    /// </summary>
    public string? ExecutorTool { get; init; }

    /// <summary>
    /// Gets the descriptor query tool name when the action supports preflight description.
    /// </summary>
    public string? DescribeTool { get; init; }

    /// <summary>
    /// Gets the structured reason code when the action cannot be executed.
    /// </summary>
    public string? UnsupportedReasonCode { get; init; }

    /// <summary>
    /// Gets the optional structured requirements.
    /// </summary>
    public IReadOnlyList<string>? Requirements { get; init; }
}
