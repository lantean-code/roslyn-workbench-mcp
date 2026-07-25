namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported Roslyn introduce-variable action through refactoring composition.
/// </summary>
internal sealed record IntroduceVariableRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected expression to introduce.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the introduce-variable leaf action to stage.
    /// </summary>
    public required IntroduceVariableKind Kind { get; init; }
}
