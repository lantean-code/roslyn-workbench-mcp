namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests replacement of an eligible method pair with a property.
/// </summary>
internal sealed record ReplaceMethodWithPropertyRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected getter method.
    /// </summary>
    public required LocationSelector Method { get; init; }

    /// <summary>
    /// Gets whether only the getter or both the getter and matching setter should be replaced.
    /// </summary>
    public required ReplaceMethodWithPropertyKind Kind { get; init; }
}
