namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record BoundedCollectionObservation
{
    public required string Path { get; init; }

    public required int ItemCount { get; init; }

    public required bool HasMore { get; init; }

    public int? TotalCount { get; init; }

    public IReadOnlyList<string> OrderedItemSha256 { get; init; } = [];
}
