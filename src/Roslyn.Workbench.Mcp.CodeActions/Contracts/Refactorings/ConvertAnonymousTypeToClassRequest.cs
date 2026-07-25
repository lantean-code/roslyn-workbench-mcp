namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported anonymous-type-to-named-type refactoring through Roslyn refactoring composition.
/// </summary>
internal sealed record ConvertAnonymousTypeToClassRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected anonymous object creation to rewrite.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the named-type variant to stage.
    /// </summary>
    public required ConvertAnonymousTypeToClassKind Kind { get; init; }
}
