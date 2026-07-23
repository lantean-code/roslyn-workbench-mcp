namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to list applicable code actions at a location.
/// </summary>
internal sealed record ListCodeActionsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the target location.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets a value indicating whether refactorings should be included.
    /// </summary>
    public bool IncludeRefactorings { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether code fixes should be included.
    /// </summary>
    public bool IncludeCodeFixes { get; init; } = true;

    /// <summary>
    /// Gets the optional diagnostic identifier filter for code fixes.
    /// </summary>
    public IReadOnlyList<string>? DiagnosticIds { get; init; }

}
