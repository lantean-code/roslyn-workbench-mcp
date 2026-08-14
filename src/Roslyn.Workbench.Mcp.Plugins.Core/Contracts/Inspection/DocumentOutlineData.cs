namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-document-outline.
/// </summary>
internal sealed record DocumentOutlineData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved document reference.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the outline root node.
    /// </summary>
    public OutlineNode? Root { get; init; }

    /// <summary>
    /// Gets a value indicating whether the hierarchy was truncated by its node or depth bound.
    /// </summary>
    public bool Truncated { get; init; }
}
