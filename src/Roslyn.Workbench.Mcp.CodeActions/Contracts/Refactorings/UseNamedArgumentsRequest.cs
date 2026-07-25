namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported use-named-arguments refactoring through Roslyn refactoring composition.
/// </summary>
internal sealed record UseNamedArgumentsRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected argument location.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets a value indicating whether trailing arguments should also receive names.
    /// </summary>
    public bool IncludeTrailingArguments { get; init; }
}
