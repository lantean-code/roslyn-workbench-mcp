namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal readonly record struct ReferenceSearchCandidate
{
    public required ReferenceOccurrence Occurrence { get; init; }

    public required ResolvedLocation ResolvedLocation { get; init; }
}
