using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to prepare a Fix All operation without staging it.
/// </summary>
internal sealed record PrepareFixAllRequest : WorkspaceBoundRequest
{
    private const int _defaultAffectedDocumentsLimit = 20;
    private const int _defaultMaxChanges = 50;

    /// <summary>
    /// The opaque originating Code Fix reference.
    /// </summary>
    [Description("The opaque originating Code Fix reference.")]
    public required Guid ActionId { get; init; }

    /// <summary>
    /// The explicit Fix All scope.
    /// </summary>
    [Description("The explicit Fix All scope.")]
    public required CodeActionFixAllScope Scope { get; init; }

    /// <summary>
    /// The maximum number of changed source documents to allow.
    /// </summary>
    [Description("The maximum number of changed source documents to allow.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxChanges)]
    public int? MaxChanges { get; init; } = _defaultMaxChanges;

    /// <summary>
    /// The maximum number of affected document identities to return.
    /// </summary>
    [Description("The maximum number of affected document identities to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAffectedDocumentsLimit)]
    public int? AffectedDocumentsLimit { get; init; } = _defaultAffectedDocumentsLimit;

    /// <summary>
    /// Gets the workspace snapshot associated with the originating Code Fix reference.
    /// </summary>
    public required SnapshotPrecondition ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the maximum changed-document count after applying the default.
    /// </summary>
    internal int EffectiveMaxChanges => ResultLimit.GetEffectiveValue(MaxChanges, _defaultMaxChanges);

    /// <summary>
    /// Gets the affected-document response limit after applying the default.
    /// </summary>
    internal int EffectiveAffectedDocumentsLimit => ResultLimit.GetEffectiveValue(
        AffectedDocumentsLimit,
        _defaultAffectedDocumentsLimit);
}
