using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-test-impact.
/// </summary>
[PublishedCollectionResponse(nameof(Tests))]
public sealed record TestImpactData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned impacted tests.
    /// </summary>
    public IReadOnlyList<TestImpactInfo> Tests { get; init; } = [];

    /// <summary>
    /// Gets the number of tests returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more tests were available.
    /// </summary>
    public bool HasMore { get; init; }
}
