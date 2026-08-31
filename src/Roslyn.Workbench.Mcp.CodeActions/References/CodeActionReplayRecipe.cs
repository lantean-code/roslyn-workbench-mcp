namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Records the stable identities and source context needed to rediscover a Code Action.
/// </summary>
internal sealed record CodeActionReplayRecipe
{
    /// <summary>
    /// Gets the kind of action to rediscover.
    /// </summary>
    public required DiscoveredActionKind Kind { get; init; }

    /// <summary>
    /// Gets the identity of the provider that produced the action.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets the action title used to select the rediscovered action.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the optional equivalence key used to distinguish related actions.
    /// </summary>
    public string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the sequence of child indexes leading to a nested action.
    /// </summary>
    public IReadOnlyList<int> ActionPath { get; init; } = [];

    /// <summary>
    /// Gets the diagnostic identifiers associated with the action.
    /// </summary>
    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    /// <summary>
    /// Gets the diagnostic identities used to rediscover the action.
    /// </summary>
    public IReadOnlyList<CodeActionDiagnosticIdentity> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the additional data needed to replay a prepared Fix All operation.
    /// </summary>
    public PreparedFixAllReplayData? PreparedFixAll { get; init; }

    /// <summary>
    /// Gets the workspace snapshot against which the action was discovered.
    /// </summary>
    public required WorkspaceSnapshotIdentity SnapshotIdentity { get; init; }

    /// <summary>
    /// Gets the path of the document containing the action target.
    /// </summary>
    public required string DocumentPath { get; init; }

    /// <summary>
    /// Gets the Roslyn project identifier containing the target document.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// Gets the target span's zero-based start position.
    /// </summary>
    public required int Start { get; init; }

    /// <summary>
    /// Gets the target span length.
    /// </summary>
    public required int Length { get; init; }
}
