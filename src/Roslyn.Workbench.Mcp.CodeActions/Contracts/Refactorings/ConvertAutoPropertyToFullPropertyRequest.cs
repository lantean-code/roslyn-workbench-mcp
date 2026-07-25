namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported auto-property to a full property through Roslyn refactoring composition.
/// </summary>
internal sealed record ConvertAutoPropertyToFullPropertyRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected auto-property declaration to rewrite.
    /// </summary>
    public required LocationSelector Selection { get; init; }
}
