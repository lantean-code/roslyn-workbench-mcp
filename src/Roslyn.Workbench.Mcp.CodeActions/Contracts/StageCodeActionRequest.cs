namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected refactoring action.
/// </summary>
internal sealed record StageCodeActionRequest : WorkspaceMutationRequest, ICodeActionReferenceRequest
{
    /// <summary>
    /// Gets the opaque action reference.
    /// </summary>
    public required Guid ActionId { get; init; }
}
