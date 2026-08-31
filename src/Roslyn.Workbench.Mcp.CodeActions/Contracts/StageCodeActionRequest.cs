using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected Code Fix or refactoring action.
/// </summary>
internal sealed record StageCodeActionRequest : WorkspaceMutationRequest, ICodeActionReferenceRequest
{
    /// <summary>
    /// The opaque action reference.
    /// </summary>
    [Description("The opaque action reference.")]
    public required Guid ActionId { get; init; }
}
