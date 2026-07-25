namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected refactoring action.
/// </summary>
internal sealed record StageCodeActionRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public required string ActionId { get; init; }
}
