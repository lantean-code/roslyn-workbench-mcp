namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one duplicate-code group.
/// </summary>
internal sealed record DuplicateCodeGroup
{
    /// <summary>
    /// Gets the number of normalized statements in the group.
    /// </summary>
    [Description("The number of normalized statements in the group.")]
    public int StatementCount { get; init; }

    /// <summary>
    /// Gets the group occurrences.
    /// </summary>
    [Description("The group occurrences.")]
    public BoundedCollection<DuplicateCodeOccurrence> Occurrences { get; init; } = BoundedCollection.Empty<DuplicateCodeOccurrence>();
}
