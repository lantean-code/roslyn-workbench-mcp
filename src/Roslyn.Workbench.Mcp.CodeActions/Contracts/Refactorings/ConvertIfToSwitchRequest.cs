namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported if-chain to a switch form through Roslyn refactoring composition.
/// </summary>
internal sealed record ConvertIfToSwitchRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected if-chain to convert.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the target switch form to stage.
    /// </summary>
    public required ConvertIfToSwitchKind Kind { get; init; }
}
