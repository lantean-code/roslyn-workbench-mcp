namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record BoundedCollectionObservation
{
    public required string Path { get; init; }

    public required int ItemCount { get; init; }

    public required bool HasMore { get; init; }

    public IReadOnlyList<string> OrderedItemSha256 { get; init; } = [];
}
