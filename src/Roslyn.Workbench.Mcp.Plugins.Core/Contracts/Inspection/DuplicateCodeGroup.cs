namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one duplicate-code group.
/// </summary>
public sealed record DuplicateCodeGroup
{
    /// <summary>
    /// Gets the number of normalized statements in the group.
    /// </summary>
    public int StatementCount { get; init; }

    /// <summary>
    /// Gets the group occurrences.
    /// </summary>
    public IReadOnlyList<DuplicateCodeOccurrence> Occurrences { get; init; } = [];
}
