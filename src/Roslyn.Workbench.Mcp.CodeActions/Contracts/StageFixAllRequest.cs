using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected code fix across a broader scope.
/// </summary>
internal sealed record StageFixAllRequest : WorkspaceBoundRequest
{
    internal const int _defaultMaxChanges = 50;

    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target scope for the fix-all operation.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the maximum number of changed source documents to allow.
    /// </summary>
    [DefaultValue(_defaultMaxChanges)]
    public int? MaxChanges { get; init; } = _defaultMaxChanges;

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
