using System.Text.Json.Serialization;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents one likely impacted test.
/// </summary>
public sealed record TestImpactInfo
{
    /// <summary>
    /// Gets the impacted test symbol.
    /// </summary>
    public SymbolReference? Test { get; init; }

    /// <summary>
    /// Gets the source location for the test, when available.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the explanatory impact reasons.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Reasons { get; init; }
}
