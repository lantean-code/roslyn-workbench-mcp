namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve the semantic outline of a document.
/// </summary>
internal sealed record GetDocumentOutlineRequest : WorkspaceBoundRequest
{
    private const int _defaultMaxDepth = 16;
    private const int _defaultNodesMaxResults = 200;
    private const int _maximumMaxDepth = 24;
    private const int _maximumNodesMaxResults = 2_000;

    /// <summary>
    /// Gets the document selector.
    /// </summary>
    [Description("Target document.")]
    public required DocumentSelector Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether member nodes should be included.
    /// </summary>
    [Description("Whether member nodes should be included.")]
    public bool IncludeMembers { get; init; } = true;

    /// <summary>
    /// Gets the maximum semantic hierarchy depth below the document root.
    /// </summary>
    [Description("The maximum semantic hierarchy depth below the document root.")]
    [Range(0, _maximumMaxDepth)]
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional maximum total number of projected outline nodes.
    /// </summary>
    [Description("The optional maximum total number of projected outline nodes.")]
    [Range(0, _maximumNodesMaxResults)]
    [DefaultValue(_defaultNodesMaxResults)]
    public int? NodesLimit { get; init; } = _defaultNodesMaxResults;

    internal int EffectiveNodesLimit => ResultLimit.GetEffectiveValue(NodesLimit, _defaultNodesMaxResults);
}
