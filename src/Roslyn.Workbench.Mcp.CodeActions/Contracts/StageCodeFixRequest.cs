namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected code fix.
/// </summary>
internal sealed record StageCodeFixRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public required string ActionId { get; init; }
}
