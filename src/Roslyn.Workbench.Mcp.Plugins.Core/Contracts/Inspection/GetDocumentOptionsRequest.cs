namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve document options.
/// </summary>
internal sealed record GetDocumentOptionsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the document selector.
    /// </summary>
    public required DocumentSelector Document { get; init; }
}
