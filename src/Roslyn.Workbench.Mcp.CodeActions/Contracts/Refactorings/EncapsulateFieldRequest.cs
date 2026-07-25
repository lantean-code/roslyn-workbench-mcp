namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests field encapsulation through Roslyn refactoring composition.
/// </summary>
internal sealed record EncapsulateFieldRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the field to encapsulate.
    /// </summary>
    public required SymbolSelector Field { get; init; }

    /// <summary>
    /// Gets a value indicating whether field references should be rewritten to use the generated property.
    /// </summary>
    public bool UpdateReferences { get; init; } = true;
}
