namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Captures a leaf Code Action and the information required to rediscover it later.
/// </summary>
internal sealed record DiscoveredCodeAction
{
    /// <summary>
    /// Gets the Roslyn action produced during discovery.
    /// </summary>
    public required CodeAction Action { get; init; }

    /// <summary>
    /// Gets the provider family that produced the action.
    /// </summary>
    public required DiscoveredActionKind Kind { get; init; }

    /// <summary>
    /// Gets the stable identifier of the originating provider.
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets the display title of the leaf action.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the source span used to discover the action.
    /// </summary>
    public required TextSpan TargetSpan { get; init; }

    /// <summary>
    /// Gets the provider equivalence key used for Code Fix and Fix All matching.
    /// </summary>
    public string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the child indexes leading from the provider's root action to this leaf.
    /// </summary>
    public IReadOnlyList<int> ActionPath { get; init; } = [];

    /// <summary>
    /// Gets the diagnostic identifiers associated with the action.
    /// </summary>
    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    /// <summary>
    /// Gets stable identities for the diagnostics used to rediscover this Code Fix.
    /// </summary>
    public IReadOnlyList<CodeActionDiagnosticIdentity> Diagnostics { get; init; } = [];

    /// <summary>
    /// Gets the Fix All scopes supported by this Code Fix.
    /// </summary>
    public IReadOnlyList<CodeActionFixAllScope> FixAllScopes { get; init; } = [];
}
