namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record CodeActionReplayRecipe
{
    public DiscoveredActionKind Kind { get; init; }

    public string ProviderId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    public IReadOnlyList<CodeActionDiagnosticIdentity> Diagnostics { get; init; } = [];

    public CodeActionFixAllScope? PreparedFixAllScope { get; init; }

    public WorkspaceSnapshotIdentity SnapshotIdentity { get; init; }

    public string DocumentPath { get; init; } = string.Empty;

    public string ProjectId { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}
