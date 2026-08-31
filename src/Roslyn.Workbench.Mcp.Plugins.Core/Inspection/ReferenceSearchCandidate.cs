namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Pairs a Roslyn reference occurrence with its canonical source location for result projection.
/// </summary>
internal readonly record struct ReferenceSearchCandidate
{
    /// <summary>
    /// Gets the Roslyn reference occurrence.
    /// </summary>
    public required ReferenceOccurrence Occurrence { get; init; }

    /// <summary>
    /// Gets the occurrence's canonical source location.
    /// </summary>
    public required ResolvedLocation ResolvedLocation { get; init; }
}
