namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-document-outline.
/// </summary>
internal sealed record DocumentOutlineData : IQueryResponse
{
    /// <summary>
    /// Gets the resolved document reference.
    /// </summary>
    [Description("The resolved document reference.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the outline root node.
    /// </summary>
    [Description("The outline root node.")]
    public OutlineNode? Root { get; init; }

    /// <summary>
    /// Gets a value indicating whether the hierarchy was truncated by its node or depth bound.
    /// </summary>
    [Description("Whether the hierarchy was truncated by its node or depth bound.")]
    public bool Truncated { get; init; }
}
