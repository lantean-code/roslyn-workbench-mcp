namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-document-outline.
/// </summary>
public sealed record DocumentOutlineData
{
    /// <summary>
    /// Gets the resolved document reference.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the outline root node.
    /// </summary>
    public OutlineNode? Root { get; init; }
}
