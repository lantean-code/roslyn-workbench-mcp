using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected code fix across a broader scope.
/// </summary>
internal sealed record StageFixAllRequest : WorkspaceMutationRequest, ICodeActionReferenceRequest
{
    private const int _defaultMaxChanges = 50;

    /// <summary>
    /// Gets the opaque action reference.
    /// </summary>
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Gets the target scope for the fix-all operation.
    /// </summary>
    public required ScopeSelector Scope { get; init; }

    /// <summary>
    /// Gets the maximum number of changed source documents to allow.
    /// </summary>
    [DefaultValue(_defaultMaxChanges)]
    public int? MaxChanges { get; init; } = _defaultMaxChanges;

    internal int EffectiveMaxChanges => Math.Max(0, MaxChanges ?? _defaultMaxChanges);
}
