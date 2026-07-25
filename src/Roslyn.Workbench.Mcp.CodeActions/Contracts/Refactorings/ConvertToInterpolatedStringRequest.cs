namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported string expression to an interpolated string through Roslyn refactoring composition.
/// </summary>
internal sealed record ConvertToInterpolatedStringRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected string expression to convert.
    /// </summary>
    public required LocationSelector Selection { get; init; }
}
