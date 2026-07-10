namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed record WorkspaceRuntimeOptions
{
    public int MaxConcurrentQueries { get; init; } = 2;

    public int DefaultMaxResults { get; init; } = 100;

    public int MaxTransactionRevisions { get; init; } = 20;

    public int MaxLoadedWorkspaces { get; init; } = 4;

    public string? StateDirectory { get; init; }
}
