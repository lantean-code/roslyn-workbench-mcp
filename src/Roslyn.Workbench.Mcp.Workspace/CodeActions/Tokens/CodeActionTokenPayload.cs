namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Tokens;

internal sealed record CodeActionTokenPayload
{
    public string Kind { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? EquivalenceKey { get; init; }

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    public string? WorkspaceId { get; init; }

    public long WorkspaceEpoch { get; init; }

    public int? TransactionRevision { get; init; }

    public string ExpiresAt { get; init; } = string.Empty;

    public string DocumentPath { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}
