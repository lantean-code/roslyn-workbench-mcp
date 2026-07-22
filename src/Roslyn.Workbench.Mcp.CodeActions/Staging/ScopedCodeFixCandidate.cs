namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed record ScopedCodeFixCandidate
{
    public required Document Document { get; init; }

    public required TextSpan DocumentSpan { get; init; }

    public required CodeFixProvider Provider { get; init; }

    public required string Title { get; init; }

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
}
