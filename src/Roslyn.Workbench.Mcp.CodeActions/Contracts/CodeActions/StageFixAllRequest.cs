namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected code fix across a broader scope.
/// </summary>
public sealed record StageFixAllRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target scope for the fix-all operation.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional maximum number of changed source documents to allow.
    /// </summary>
    public int? MaxChanges { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
