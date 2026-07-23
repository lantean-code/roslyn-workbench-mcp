namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported Roslyn introduce-variable action through refactoring composition.
/// </summary>
internal sealed record IntroduceVariableRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected expression to introduce.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the introduce-variable leaf action to stage.
    /// </summary>
    public IntroduceVariableKind Kind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
