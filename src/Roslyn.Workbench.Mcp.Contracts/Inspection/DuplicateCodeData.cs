namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-duplicate-code.
/// </summary>
public sealed record DuplicateCodeData
{
    /// <summary>
    /// Gets the returned duplicate groups.
    /// </summary>
    public IReadOnlyList<DuplicateCodeGroup> Groups { get; init; } = [];

    /// <summary>
    /// Gets the number of groups returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more groups were available.
    /// </summary>
    public bool HasMore { get; init; }
}
