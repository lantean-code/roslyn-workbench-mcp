namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests that selected members are added to one eligible constructor.
/// </summary>
internal sealed record AddConstructorParametersRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected fields or properties to add as constructor parameters.
    /// </summary>
    public required LocationSelector Members { get; init; }

    /// <summary>
    /// Gets whether the generated parameters are required or optional.
    /// </summary>
    public required AddConstructorParametersKind Kind { get; init; }
}
