namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record CodeActionReplayRecipe
{
    public required DiscoveredActionKind Kind { get; init; }

    public required string ProviderId { get; init; }

    public required string Title { get; init; }

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    public IReadOnlyList<CodeActionDiagnosticIdentity> Diagnostics { get; init; } = [];

    public PreparedFixAllReplayData? PreparedFixAll { get; init; }

    public required WorkspaceSnapshotIdentity SnapshotIdentity { get; init; }

    public required string DocumentPath { get; init; }

    public required string ProjectId { get; init; }

    public required int Start { get; init; }

    public required int Length { get; init; }
}
