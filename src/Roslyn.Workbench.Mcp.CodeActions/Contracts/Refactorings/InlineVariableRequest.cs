namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests inlining of a local variable through Roslyn refactoring composition.
/// </summary>
internal sealed record InlineVariableRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the symbol selector for the local variable to inline.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether the declaration should be removed after inlining.
    /// </summary>
    public bool RemoveDeclaration { get; init; } = true;
}
