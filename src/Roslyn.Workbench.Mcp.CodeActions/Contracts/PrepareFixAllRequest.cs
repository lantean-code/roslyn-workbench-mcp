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
    /// Gets the opaque originating Code Fix reference.
    /// </summary>
    [Description("The opaque originating Code Fix reference.")]
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Gets the explicit Fix All scope.
    /// </summary>
    [Description("The explicit Fix All scope.")]
    public required CodeActionFixAllScope Scope { get; init; }

    /// <summary>
    /// Gets the maximum number of changed source documents to allow.
    /// </summary>
    [Description("The maximum number of changed source documents to allow.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMaxChanges)]
    public int? MaxChanges { get; init; } = _defaultMaxChanges;

    /// <summary>
    /// Gets the maximum number of affected document identities to return.
    /// </summary>
    [Description("The maximum number of affected document identities to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultAffectedDocumentsLimit)]
    public int? AffectedDocumentsLimit { get; init; } = _defaultAffectedDocumentsLimit;

    /// <summary>
    /// Gets the expected Workspace snapshot.
    /// </summary>
    public required SnapshotPrecondition ExpectedSnapshot { get; init; }

    internal int EffectiveMaxChanges => ResultLimit.GetEffectiveValue(MaxChanges, _defaultMaxChanges);

    internal int EffectiveAffectedDocumentsLimit => ResultLimit.GetEffectiveValue(
        AffectedDocumentsLimit,
        _defaultAffectedDocumentsLimit);
}
