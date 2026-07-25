namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests a future move of a selected type into its own Roslyn-chosen file within the same project.
/// </summary>
internal sealed record MoveTypeToFileRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the source type to move into its own document.
    /// </summary>
    public required SymbolSelector Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the moved declaration should keep its current namespace.
    /// </summary>
    public bool PreserveNamespace { get; init; } = true;
}
