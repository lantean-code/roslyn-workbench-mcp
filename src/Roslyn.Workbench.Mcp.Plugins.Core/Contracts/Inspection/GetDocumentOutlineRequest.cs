namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve the semantic outline of a document.
/// </summary>
internal sealed record GetDocumentOutlineRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether member nodes should be included.
    /// </summary>
    public bool IncludeMembers { get; init; } = true;
}
