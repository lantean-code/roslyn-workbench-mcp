namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record DiscoveredCodeAction
{
    public required CodeAction Action { get; init; }

    public required DiscoveredActionKind Kind { get; init; }

    public required string ProviderId { get; init; }

    public required string Title { get; init; }

    public required CodeActionDescriptorEntry Descriptor { get; init; }

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
}
