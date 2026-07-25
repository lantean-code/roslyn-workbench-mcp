namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

/// <summary>
/// Describes a host-owned replayable code-action selection and staging request.
/// </summary>
internal sealed record ReplayCodeActionRequest
{
    /// <summary>
    /// Gets the location selector used to discover candidate refactorings.
    /// </summary>
    public required LocationSelector Location { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot for the operation.
    /// </summary>
    public required SnapshotPrecondition ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the optional provider identity to require.
    /// </summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Gets the optional action title to require.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional action-title prefix to require.
    /// </summary>
    public string? TitleStartsWith { get; init; }

    /// <summary>
    /// Gets the optional action-title fragment that must not appear.
    /// </summary>
    public string? TitleDoesNotContain { get; init; }

    /// <summary>
    /// Gets the optional Roslyn equivalence key to require.
    /// </summary>
    public string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the optional flattened nested-action path to require.
    /// </summary>
    public IReadOnlyList<int>? ActionPath { get; init; }
}
