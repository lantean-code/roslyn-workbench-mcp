namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Describes one applicable code action or code fix.
/// </summary>
public sealed record CodeActionInfo
{
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
    /// Gets the optional structured requirements.
    /// </summary>
    public IReadOnlyList<string>? Requirements { get; init; }
}
