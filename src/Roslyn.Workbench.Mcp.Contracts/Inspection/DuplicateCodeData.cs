using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-duplicate-code.
/// </summary>
public sealed record DuplicateCodeData
{
    /// <summary>
    /// Gets the returned duplicate groups.
    /// </summary>
    public BoundedCollection<DuplicateCodeGroup> Groups { get; init; } = BoundedCollection<DuplicateCodeGroup>.Empty();
}
