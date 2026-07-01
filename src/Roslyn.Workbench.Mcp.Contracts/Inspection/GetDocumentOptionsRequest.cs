using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve document options.
/// </summary>
public sealed record GetDocumentOptionsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public DocumentSelector? Document { get; init; }
}
