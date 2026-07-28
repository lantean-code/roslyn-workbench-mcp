namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record DiscoveredCodeAction
{
    public required CodeAction Action { get; init; }

    public required DiscoveredActionKind Kind { get; init; }

    public required string ProviderId { get; init; }

    public required string Title { get; init; }

    public required CodeActionDescriptorEntry Descriptor { get; init; }

    public required TextSpan TargetSpan { get; init; }

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    public IReadOnlyList<CodeActionDiagnosticIdentity> Diagnostics { get; init; } = [];

    public IReadOnlyList<CodeActionFixAllScope> FixAllScopes { get; init; } = [];
}
