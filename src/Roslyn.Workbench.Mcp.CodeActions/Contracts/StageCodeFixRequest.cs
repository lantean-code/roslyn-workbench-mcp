namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected code fix.
/// </summary>
internal sealed record StageCodeFixRequest : WorkspaceMutationRequest, ICodeActionReferenceRequest
{
    /// <summary>
    /// Gets the opaque action reference.
    /// </summary>
    public required Guid ActionId { get; init; }
}
